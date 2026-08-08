using MicroLearning.Models;
using MicroLearning.Models.Context;

namespace MicroLearning.Services
{
    public class TopicService
    {
        private readonly Supabase.Client _db;
        private readonly string _supabaseUrl;
        private readonly string _supabaseKey;

        public TopicService(Supabase.Client db, HttpClient httpClient, IConfiguration configuration)
        {
            _db = db;
            _supabaseUrl = configuration["SupabaseUrl"] ?? string.Empty;
            _supabaseKey = configuration["SupabaseKey"] ?? string.Empty;
        }

        public async Task<List<Topic?>> GetUserTopics(Guid UserId)
        {
            List<Topic?> response = new List<Topic?>();

            try
            {
                List<UserTopics> listUserTopics = (await _db.From<UserTopics>().Where(x => x.UserId == UserId).Get()).Models;

                response = listUserTopics.Where(x => x.Topic != null).Select(x => x.Topic).OrderBy(x => x.Name).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TopicService Error]: {ex.Message}");
            }

            return response;
        }

        public async Task<List<Topic?>> AddUserTopic(Guid UserId, string TopicName)
        {
            try
            {
                string cleanedName = TopicName.Trim();

                Topic? topic = (await _db.From<Topic>().Filter(x => x.Name, Supabase.Postgrest.Constants.Operator.ILike, cleanedName).Get()).Model;
                if (topic == null)
                {
                    topic = new Topic()
                    {
                        Name = TopicName.Trim(),
                        CreatedAt = DateTime.Now
                    };

                    await _db.From<Topic>().Upsert(topic);
                }

                UserTopics userTopic = new UserTopics()
                {
                    UserId = UserId,
                    TopicId = topic.Id
                };

                await _db.From<UserTopics>().Upsert(userTopic);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TopicService Error]: {ex.Message}");
            }

            return await GetUserTopics(UserId);
        }

        public async Task<List<Topic?>> AddUserTopic(Guid UserId, Guid TopicId)
        {
            try
            {
                Topic? topic = (await _db.From<Topic>().Where(x => x.Id == TopicId).Get()).Model;
                if (topic != null)
                {
                    UserTopics userTopic = new UserTopics()
                    {
                        UserId = UserId,
                        TopicId = topic.Id
                    };

                    await _db.From<UserTopics>().Upsert(userTopic);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TopicService Error]: {ex.Message}");
            }

            return await GetUserTopics(UserId);
        }

        public async Task<List<Topic?>> RemoveUserTopic(Guid UserId, Guid TopicId)
        {
            try
            {
                UserTopics? userTopic = (await _db.From<UserTopics>().Where(x => x.UserId == UserId).Where(x => x.TopicId == TopicId).Get()).Model;
                if (userTopic != null)
                {
                    await _db.From<UserTopics>().Delete(userTopic);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TopicService Error]: {ex.Message}");
            }

            return await GetUserTopics(UserId);
        }
    }
}
