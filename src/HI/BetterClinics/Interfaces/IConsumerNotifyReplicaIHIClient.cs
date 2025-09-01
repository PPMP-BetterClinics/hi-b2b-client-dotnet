using System.Threading.Tasks;
using nehta.mcaR32.ConsumerNotifyReplicaIHI;

namespace Nehta.VendorLibrary.HI
{
    public interface IConsumerNotifyReplicaIHIClient : ISoapClient
    {
        /// <summary>
        /// Notify Replica IHI service call.
        /// </summary>
        notifyReplicaIHIResponse NotifyReplicaIhi(notifyReplicaIHI request);

        /// <summary>
        /// Asynchronous implementation of <see cref="notifyReplicaIHI" />.
        /// </summary>
        Task<notifyReplicaIHIResponse> NotifyReplicaIhiAsync(notifyReplicaIHI request);
    }
}