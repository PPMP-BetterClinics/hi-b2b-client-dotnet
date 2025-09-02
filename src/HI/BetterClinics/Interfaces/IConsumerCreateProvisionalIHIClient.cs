using System.Threading.Tasks;
using nehta.mcaR3.ConsumerCreateProvisionalIHI;

namespace Nehta.VendorLibrary.HI
{
    public interface IConsumerCreateProvisionalIHIClient : ISoapClient
    {
        /// <summary>
        /// Create a Provisional IHI service call.
        /// </summary>
        createProvisionalIHIResponse CreateProvisionalIhi(createProvisionalIHI request);

        /// <summary>
        /// Asynchronous implementation of <see cref="CreateProvisionalIhi" />.
        /// </summary>
        Task<createProvisionalIHIResponse> CreateProvisionalIhiAsync(createProvisionalIHI request);
    }
}