using System.Threading.Tasks;

namespace BookKnowledge.Gateway.Services
{
    public interface ISecretProvider
    {
        string CurrentSecret { get; }
        Task<string> GetSecretAsync();
        void SetSecret(string secret);
    }

    public class InMemorySecretProvider : ISecretProvider
    {
        private string _secret;

        public InMemorySecretProvider(string initialSecret)
        {
            _secret = initialSecret ?? string.Empty;
        }

        public string CurrentSecret => _secret;

        public Task<string> GetSecretAsync() => Task.FromResult(_secret);

        public void SetSecret(string secret)
        {
            _secret = secret ?? string.Empty;
        }
    }
}
