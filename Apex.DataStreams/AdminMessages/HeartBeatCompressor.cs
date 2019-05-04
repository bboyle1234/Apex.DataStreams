using Apex.ValueCompression;
using Apex.ValueCompression.Compressors;
using System.IO;

namespace Apex.DataStreams.AdminMessages {
    internal sealed class HeartBeatCompressor : CompressorBase<HeartBeat> {
        public override void Compress(Stream stream, HeartBeat value) { }
        public override HeartBeat Decompress(Stream stream) => new HeartBeat();
    }
}
