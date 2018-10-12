namespace Yutbube.Models
{
    public class QueueMessagePayload
    {
        public StorageItem Video { get; }
        public string ClientId { get; }

        public QueueMessagePayload(StorageItem video, string clientId)
        {
            Video = video;
            ClientId = clientId;
        }
    }
}