using System.Threading.Tasks;
using nehta.mcaR302.ConsumerResolveProvisionalIHI;

namespace Nehta.VendorLibrary.HI
{
    public interface IConsumerResolveProvisionalIHIClient : ISoapClient
    {
        /// <summary>
        /// Resolve a Provisional IHI service call.
        /// </summary>
        resolveProvisionalIHIResponse ResolveProvisionalIhi(resolveProvisionalIHI request);

        /// <summary>
        /// Asynchronous implementation of <see cref="ResolveProvisionalIhi" />.
        /// </summary>
        Task<resolveProvisionalIHIResponse> ResolveProvisionalIhiAsync(resolveProvisionalIHI request);
    }
}