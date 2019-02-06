using Apex.DataStreams.Encoding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams.Operations {

    internal sealed class SendOperation : SucceedFailOperation {

        public readonly MessageEnvelope Envelope;

        public SendOperation(MessageEnvelope envelope) {
            Envelope = envelope;
        }

    }
}
