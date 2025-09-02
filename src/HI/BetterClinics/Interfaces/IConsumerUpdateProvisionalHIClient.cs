using System.Threading.Tasks;
using nehta.mcaR3.ConsumerUpdateProvisionalIHI;

namespace Nehta.VendorLibrary.HI
{
    public interface IConsumerUpdateProvisionalIHIClient : ISoapClient
    {
        /// <summary>
        /// Update a Provisional IHI service call.
        /// </summary>
        updateProvisionalIHIResponse UpdateProvisionalIhi(updateProvisionalIHI request);

        /// <summary>
        /// Asynchronous implementation of <see cref="UpdateProvisionalIhi" />.
        /// </summary>
        Task<updateProvisionalIHIResponse> UpdateProvisionalIhiAsync(updateProvisionalIHI request);
    }
}