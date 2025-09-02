using System.Threading.Tasks;
using nehta.mcaR32.ConsumerUpdateIHI;

namespace Nehta.VendorLibrary.HI
{
    public interface IConsumerUpdateIHIClient : ISoapClient
    {
        /// <summary>
        /// Update a IHI service call.
        /// </summary>
        updateIHIResponse UpdateIhi(updateIHI request);

        /// <summary>
        /// Asynchronous implementation of <see cref="UpdateIhi" />.
        /// </summary>
        Task<updateIHIResponse> UpdateIhiAsync(updateIHI request);
    }
}