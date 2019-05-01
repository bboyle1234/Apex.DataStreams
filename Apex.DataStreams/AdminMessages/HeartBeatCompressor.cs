using Apex.ValueCompression;
using System.IO;

namespace Apex.DataStreams.AdminMessages {
    internal sealed class HeartBeatCompressor : ICompressor<HeartBeat>, IDecompressor<HeartBeat> {
        public void Compress(Stream stream, HeartBeat value) { }
        public void Compress(Stream stream, object value) { }
        public HeartBeat Decompress(Stream stream) => new HeartBeat();
        object IDecompressor.Decompress(Stream stream) => this.Decompress(stream);
    }
}
