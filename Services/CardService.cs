using MicroLearning.Models;
using MicroLearning.Models.Context;
using Microsoft.AspNetCore.Components.WebAssembly.Http; 
using System.Runtime.CompilerServices;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MicroLearning.Services
{
    public class CardService
    {
        private readonly Supabase.Client _db;
        private readonly HttpClient _httpClient;
        private readonly string _supabaseUrl;
        private readonly string _supabaseKey;

        public CardService(Supabase.Client db, HttpClient httpClient, IConfiguration configuration)
        {
            _db = db;
            _httpClient = httpClient;
            _supabaseUrl = configuration["SupabaseUrl"] ?? string.Empty;
            _supabaseKey = configuration["SupabaseKey"] ?? string.Empty;
        }

        public async Task<List<CardModel>> GetFeedCards(Guid UserId, List<Guid>? excludeCardIds = null)
        {
            List<CardModel> response = new List<CardModel>();
            excludeCardIds ??= new List<Guid>();

            try
            {
                // 1. Card già assegnate all'utente e non lette
                List<UserCards> listUserCards = (await _db.From<UserCards>()
                    .Where(x => x.UserId == UserId)
                    .Where(x => !x.IsPersonalFeed)
                    .Where(x => x.ReadAt == null)
                    .Where(x => !excludeCardIds.Contains(x.CardId))
                    .Get()).Models;

                if (listUserCards.Any(x => x.Card != null))
                {
                    var rawList = MapToCardModelList(listUserCards);
                    return ShuffleAndDistributeTopics(rawList, maxConsecutive: 2);
                }

                // 2. Recupera le card lette da escludere
                List<UserCards> listUserCardsRead = (await _db.From<UserCards>()
                    .Where(x => x.UserId == UserId)
                    .Where(x => x.ReadAt != null)
                    .Get()).Models;

                List<Guid> ignoreCardIds = listUserCardsRead.Select(x => x.CardId)
                    .Concat(excludeCardIds)
                    .Distinct()
                    .ToList();

                // 💡 TRUCCO RANDOM DB: Aumentiamo il limite da 10 a 30 per estrarre un pool più vario di topic,
                // poi facciamo uno shuffle vero in memoria prendendone 10.
                List<Card> listCards = (await _db.From<Card>()
                    .Where(x => !ignoreCardIds.Contains(x.Id))
                    .Limit(30) // 👈 estraiamo un buffer più grande per avere più varietà di topic
                    .Get()).Models;

                if (listCards.Any())
                {
                    // Mescoliamo le card estratte e prendiamo solo le prime 10
                    var randomizedCards = listCards.OrderBy(_ => Random.Shared.Next()).Take(10).ToList();

                    List<UserCards> newCardsToInsert = randomizedCards.Select(x => new UserCards
                    {
                        CardId = x.Id,
                        UserId = UserId,
                        ReadAt = null
                    }).ToList();

                    await _db.From<UserCards>().Upsert(newCardsToInsert);

                    var mappedList = randomizedCards.Select(x => new CardModel
                    {
                        Id = x.Id,
                        TopicId = x.TopicId,
                        TopicName = x.Topic?.Name ?? string.Empty,
                        Title = x.Title,
                        Subtitle = x.Subtitle,
                        Body = x.Body,
                        DeepDive = x.DeepDive
                    }).ToList();

                    return ShuffleAndDistributeTopics(mappedList, maxConsecutive: 2);
                }

                // 3. Se non ci sono card, genera via AI
                List<Topic> listRandomTopics = await _db.Rpc<List<Topic>>("get_random_topics", new Dictionary<string, object> { { "limit_count", 3 } }) ?? new();

                var listTopicItems = listRandomTopics
                    .Where(t => t.Name != null)
                    .Select(t => new TopicItem { TopicId = t.Id, TopicName = t.Name!, Count = 2 })
                    .ToList();

                CardsReq cardsReq = new CardsReq { UserId = UserId, Requests = listTopicItems };
                List<Card> listGeneratedCards = await GeminiGenerateCards(cardsReq);

                if (listGeneratedCards.Any())
                {
                    List<UserCards> listGeneratedUserCards = listGeneratedCards.Select(x => new UserCards
                    {
                        CardId = x.Id,
                        UserId = UserId,
                        ReadAt = null
                    }).ToList();

                    await _db.From<UserCards>().Upsert(listGeneratedUserCards);

                    var mappedGeneratedList = listGeneratedCards.Select(x => new CardModel
                    {
                        Id = x.Id,
                        TopicId = x.TopicId,
                        TopicName = x.Topic?.Name ?? string.Empty,
                        Title = x.Title,
                        Subtitle = x.Subtitle,
                        Body = x.Body,
                        DeepDive = x.DeepDive
                    }).ToList();

                    return ShuffleAndDistributeTopics(mappedGeneratedList, maxConsecutive: 2);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CardService Error]: {ex.Message}");
            }

            return response;
        }

        public async Task<List<CardModel>> GetPersonalFeedCards(Guid UserId, List<Guid>? excludeCardIds = null)
        {
            List<CardModel> response = new List<CardModel>();
            excludeCardIds ??= new List<Guid>();

            try
            {
                List<UserTopics> listUserTopics = (await _db.From<UserTopics>().Where(x => x.UserId == UserId).Get()).Models;

                // 1. Cerca prima se l'utente ha card non lette già assegnate, ESCLUDENDO quelle già in memoria
                List<UserCards> listUserCards = (await _db.From<UserCards>()
                    .Where(x => x.UserId == UserId)
                    .Where(x => x.IsPersonalFeed)
                    .Where(x => x.ReadAt == null)
                    .Where(x => !excludeCardIds.Contains(x.CardId)) // 👈 ESCLUDI QUELLE GIÀ SULLO SCHERMO
                    .Get()).Models;

                if (listUserCards.Any(x => x.Card != null))
                {
                    var rawList = MapToCardModelList(listUserCards);
                    return ShuffleAndDistributeTopics(rawList, maxConsecutive: 2);
                }

                // 2. Recupera le card lette per non riproporle
                List<UserCards> listUserCardsRead = (await _db.From<UserCards>()
                    .Where(x => x.UserId == UserId)
                    .Where(x => x.ReadAt != null)
                    .Get()).Models;

                // Uniamo le card lette su DB a quelle attualmente visualizzate nello schermo
                List<Guid> ignoreCardIds = listUserCardsRead.Select(x => x.CardId)
                    .Concat(excludeCardIds)
                    .Distinct()
                    .ToList();

                // Recupera card dal DB che l'utente non ha mai visto né ha in memoria
                List<Card> listCards = (await _db.From<Card>()
                    .Where(x => !ignoreCardIds.Contains(x.Id)) // 👈 ESCLUSIONE TOTALE
                    .Where(x => listUserTopics.Select(y => y.TopicId).Contains(x.TopicId))
                    .Limit(10)
                    .Get()).Models;

                if (listCards.Any())
                {
                    List<UserCards> newCardsToInsert = listCards.Select(x => new UserCards
                    {
                        CardId = x.Id,
                        UserId = UserId,
                        IsPersonalFeed = true,
                        ReadAt = null
                    }).ToList();

                    await _db.From<UserCards>().Upsert(newCardsToInsert);

                    var mappedList = listCards.Select(x => new CardModel
                    {
                        Id = x.Id,
                        TopicId = x.TopicId,
                        TopicName = x.Topic?.Name ?? string.Empty,
                        Title = x.Title,
                        Subtitle = x.Subtitle,
                        Body = x.Body,
                        DeepDive = x.DeepDive
                    }).ToList();

                    return ShuffleAndDistributeTopics(mappedList, maxConsecutive: 2);
                }

                // 3. Se non ci sono più card sul DB, usa la RPC per prendere i topic random dell'utente e generare con AI
                Dictionary<string, object> randomTopicsParameters = new Dictionary<string, object>
                {
                    { "target_user_id", UserId },
                    { "limit_count", 3 }
                };
                List<Topic> listRandomTopics = await _db.Rpc<List<Topic>>("get_random_user_topics", randomTopicsParameters) ?? new List<Topic>();

                List<TopicItem>? listTopicItems = listRandomTopics
                    .Where(t => t.Name != null)
                    .Select(t => new TopicItem { TopicId = t.Id, TopicName = t.Name!, Count = 2 })
                    .ToList();

                CardsReq cardsReq = new CardsReq { UserId = UserId, Requests = listTopicItems };
                List<Card> listGeneratedCards = await GeminiGenerateCards(cardsReq);

                if (listGeneratedCards.Any())
                {
                    List<UserCards> listGeneratedUserCards = listGeneratedCards.Select(x => new UserCards
                    {
                        CardId = x.Id,
                        UserId = UserId,
                        IsPersonalFeed = true,
                        ReadAt = null
                    }).ToList();

                    await _db.From<UserCards>().Upsert(listGeneratedUserCards);

                    var mappedGeneratedList = listGeneratedCards.Select(x => new CardModel
                    {
                        Id = x.Id,
                        TopicId = x.TopicId,
                        TopicName = x.Topic?.Name ?? string.Empty,
                        Title = x.Title,
                        Subtitle = x.Subtitle,
                        Body = x.Body,
                        DeepDive = x.DeepDive
                    }).ToList();

                    return ShuffleAndDistributeTopics(mappedGeneratedList, maxConsecutive: 2);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CardService Error]: {ex.Message}");
            }

            return response;
        }

        /// <summary>
        /// Esegue uno shuffle randomico reale distribuendo i topic per evitare più di 'maxConsecutive' card identiche di fila.
        /// </summary>
        private List<CardModel> ShuffleAndDistributeTopics(List<CardModel> sourceCards, int maxConsecutive = 2)
        {
            if (sourceCards == null || sourceCards.Count <= maxConsecutive)
                return sourceCards ?? new List<CardModel>();

            // 1. Raggruppa per TopicId e mescola ogni gruppo internamente con Random.Shared
            var topicGroups = sourceCards
                .GroupBy(c => c.TopicId)
                .Select(g => new Queue<CardModel>(g.OrderBy(_ => Random.Shared.Next())))
                .OrderByDescending(q => q.Count) // I topic con più card hanno la priorità di inserimento
                .ToList();

            var result = new List<CardModel>();
            int totalCards = sourceCards.Count;

            while (result.Count < totalCards)
            {
                bool addedInThisRound = false;

                // Cerca prima tra i gruppi che NON violano il vincolo dei consecutivi
                foreach (var group in topicGroups)
                {
                    if (group.Count == 0) continue;

                    var candidate = group.Peek();

                    // Verifica quante card dello stesso topic ci sono attualmente alla fine del risultato
                    int consecutiveCount = 0;
                    for (int i = result.Count - 1; i >= 0; i--)
                    {
                        if (result[i].TopicId == candidate.TopicId)
                            consecutiveCount++;
                        else
                            break;
                    }

                    // Se l'inserimento rispetta il limite, estrai la card dal gruppo e aggiungila
                    if (consecutiveCount < maxConsecutive)
                    {
                        result.Add(group.Dequeue());
                        addedInThisRound = true;
                        break;
                    }
                }

                // FALLBACK: Se tutti i gruppi disponibili violerebbero il limite 
                // (es. rimangono solo card dell'unico topic rimasto), prendiamo dal gruppo più numeroso.
                if (!addedInThisRound)
                {
                    var largestGroup = topicGroups.Where(g => g.Count > 0).OrderByDescending(g => g.Count).FirstOrDefault();
                    if (largestGroup != null && largestGroup.Count > 0)
                    {
                        result.Add(largestGroup.Dequeue());
                    }
                    else
                    {
                        break; // Sicurezza per evitare loop infiniti
                    }
                }

                // Re-ordina i gruppi in base a chi ha ancora più elementi per bilanciare la distribuzione
                topicGroups = topicGroups.OrderByDescending(q => q.Count).ToList();
            }

            return result;
        }

        public async Task MarkCardAsRead(Guid UserId, Guid CardId)
        {
            try
            {
                UserCards? userCard = (await _db.From<UserCards>().Where(x => x.UserId == UserId).Where(x => x.CardId == CardId).Where(x => x.ReadAt == null).Get()).Model;
                if (userCard != null)
                {
                    userCard.ReadAt = DateTime.UtcNow;
                    await _db.From<UserCards>().Upsert(userCard);
                }
            }
            catch (Exception ex)
            {

            }
        }

        // Helper per evitare duplicazione di codice di mappatura
        private List<CardModel> MapToCardModelList(List<UserCards> userCards)
        {
            return userCards
                .Where(x => x.Card != null)
                .Select(x => new CardModel
                {
                    Id = x.CardId,
                    TopicId = x.Card!.TopicId,
                    TopicName = x.Card.Topic?.Name ?? string.Empty,
                    Title = x.Card.Title,
                    Subtitle = x.Card.Subtitle,
                    Body = x.Card.Body,
                    DeepDive = x.Card.DeepDive,
                    KeyWords = x.Card.KeyWords,
                }).ToList();
        }

        public async Task<List<Card>> GeminiGenerateCards(CardsReq cardReq)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, _supabaseUrl + "/functions/v1/generate-cards-new")
                {
                    Content = JsonContent.Create(cardReq)
                };

                // 1. Apikey rimane sempre la anon key del progetto
                request.Headers.Add("apikey", _supabaseKey);

                // 2. Recupera l'AccessToken dell'utente correntemente loggato
                var userToken = _db.Auth.CurrentSession?.AccessToken;

                if (string.IsNullOrEmpty(userToken))
                {
                    Console.WriteLine("[Errore Auth]: Nessun utente loggato o token di sessione non trovato.");
                    return new List<Card>();
                }

                // 3. Passa il token JWT dell'utente invece della _supabaseKey
                request.Headers.Add("Authorization", $"Bearer {userToken}");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Errore HTTP {response.StatusCode}]: {errorBody}");
                    return new List<Card>();
                }
                var st = await response.Content.ReadAsStringAsync();
                return await response.Content.ReadFromJsonAsync<List<Card>>() ?? new List<Card>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Errore HTTP]: {ex.Message}");
                return new List<Card>();
            }
        }

        public async IAsyncEnumerable<string> GeminiGenerateDeepDiveStream(Guid cardId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var userToken = _db.Auth.CurrentSession?.AccessToken;

            if (string.IsNullOrEmpty(userToken))
            {
                Console.WriteLine("[Errore Auth]: Nessun utente loggato.");
                yield break;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/functions/v1/generate-deep-dive")
            {
                Content = JsonContent.Create(new CardDeepDiveReq { CardId = cardId })
            };

            request.SetBrowserResponseStreamingEnabled(true);

            request.Headers.Add("apikey", _supabaseKey);
            request.Headers.Add("Authorization", $"Bearer {userToken}");

            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Errore HTTP DeepDive]: {ex.Message}");
                yield break;
            }

            using (response)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                    {
                        continue;
                    }

                    var jsonPayload = line["data: ".Length..].Trim();

                    if (jsonPayload == "[DONE]")
                    {
                        break;
                    }

                    string? extractedText = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(jsonPayload);
                        extractedText = doc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();
                    }
                    catch { }

                    if (!string.IsNullOrEmpty(extractedText))
                    {
                        yield return extractedText;
                    }
                }
            }
        }

    }
}
