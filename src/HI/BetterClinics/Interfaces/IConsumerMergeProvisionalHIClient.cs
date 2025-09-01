using System.Threading.Tasks;
using nehta.mcaR3.ConsumerMergeProvisionalIHI;

namespace Nehta.VendorLibrary.HI
{
    public interface IConsumerMergeProvisionalIHIClient : ISoapClient
    {
        /// <summary>
        /// Merge a Provisional IHI service call.
        /// </summary>
        mergeProvisionalIHIResponse MergeProvisionalIhi(mergeProvisionalIHIRequest request);
        
        /// <summary>
        /// Asynchronous implementation of <see cref="MergeProvisionalIhi" />.
        /// </summary>
        Task<mergeProvisionalIHIResponse> MergeProvisionalIhiAsync(mergeProvisionalIHIRequest request);
    }
}