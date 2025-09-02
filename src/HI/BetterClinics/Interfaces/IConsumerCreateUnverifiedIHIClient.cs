using System.Threading.Tasks;
using nehta.mcaR302.ConsumerCreateUnverifiedIHI;

namespace Nehta.VendorLibrary.HI
{
    public interface IConsumerCreateUnverifiedIHIClient : ISoapClient
    {
        /// <summary>
        /// Create a Unverified IHI service call.
        /// </summary>
        createUnverifiedIHIResponse CreateUnverifiedIhi(createUnverifiedIHI request);

        /// <summary>
        /// Asynchronous implementation of <see cref="CreateUnverifiedIhi" />.
        /// </summary>
        Task<createUnverifiedIHIResponse> CreateUnverifiedIhiAsync(createUnverifiedIHI request);
    }
}