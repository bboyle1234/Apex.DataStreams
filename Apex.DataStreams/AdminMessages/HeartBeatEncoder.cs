using Apex.PipeEncoding;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Apex.DataStreams.AdminMessages {

    internal sealed class HeartBeatEncoder : EncoderBase<HeartBeat> {

        public override ValueTask<HeartBeat> Decode(PipeReader reader)
            => new ValueTask<HeartBeat>(new HeartBeat());

        public override void Encode(PipeWriter writer, HeartBeat value) { }
    }
}
