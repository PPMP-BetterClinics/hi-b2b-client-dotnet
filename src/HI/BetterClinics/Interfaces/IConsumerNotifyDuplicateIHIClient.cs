using System.Threading.Tasks;
using nehta.mcaR32.ConsumerNotifyDuplicateIHI;

namespace Nehta.VendorLibrary.HI
{
    public interface IConsumerNotifyDuplicateIHIClient : ISoapClient
    {
        /// <summary>
        /// Notify Duplicate IHI service call.
        /// </summary>
        notifyDuplicateIHIResponse NotifyDuplicateIhi(notifyDuplicateIHI request);

        /// <summary>
        /// Asynchronous implementation of <see cref="notifyDuplicateIHI" />.
        /// </summary>
        Task<notifyDuplicateIHIResponse> NotifyDuplicateIhiAsync(notifyDuplicateIHI request);
    }
}