using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinPhone.Mail.Protocols.Transport
{
    public class BufferedReadStream : Stream
    {
        private Stream _innerStream;
        private byte[] _buffer;
        private int _offset;
        private int _count;

        public BufferedReadStream(Stream innerStream)
        {
            _innerStream = innerStream;
            _buffer = new byte[1024];
            _offset = 0;
            _count = 0;
        }

        public override bool CanRead
        {
            get { return _innerStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _innerStream.CanSeek; }
        }

        public override bool CanTimeout
        {
            get { return _innerStream.CanTimeout; }
        }

        public override bool CanWrite
        {
            get { return _innerStream.CanWrite; }
        }

        public override long Length
        {
            get { return _innerStream.Length + _count; }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int ReadTimeout
        {
            get { return _innerStream.ReadTimeout; }
            set { _innerStream.ReadTimeout = value; }
        }

        public override int WriteTimeout
        {
            get { return _innerStream.WriteTimeout; }
            set { _innerStream.WriteTimeout = value; }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int ReadByte()
        {
            if (_count > 0)
            {
                int value = _buffer[_offset];
                _offset++;
                _count--;
                return value;
            }

            return _innerStream.ReadByte();
        }

        private static void ValidateReadArgs(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if (offset < 0 || offset >= buffer.Length)
            {
                throw new ArgumentOutOfRangeException("offset", offset, string.Empty);
            }
            if (count <= 0 || count > buffer.Length - offset)
            {
                throw new ArgumentOutOfRangeException("count", count, string.Empty);
            }                 
        }

        private int CopyFromBuffer(byte[] buffer, int offset, int count)
        {
            if (_count > 0)
            {
                int bytesToCopy = Math.Min(count, _count);
                Array.Copy(_buffer, _offset, buffer, offset, bytesToCopy);
                _offset += bytesToCopy;
                _count -= bytesToCopy;
                return bytesToCopy;
            }
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateReadArgs(buffer, offset, count);
            if (_count > 0)
            {
                return CopyFromBuffer(buffer, offset, count);
            }

            return _innerStream.Read(buffer, offset, count);
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            ValidateReadArgs(buffer, offset, count);
            if (_count > 0)
            {
                int read = CopyFromBuffer(buffer, offset, count);

                throw new NotImplementedException(); // TODO:
            }

            return _innerStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return _innerStream.EndRead(asyncResult);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            ValidateReadArgs(buffer, offset, count);
            if (_count > 0)
            {
                int read = CopyFromBuffer(buffer, offset, count);
                return Task.FromResult(read);
            }
        
            return _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public async Task<int> EnsureBufferAsync()
        {
            if (_count == 0)
            {
                _offset = 0;
                _count = await _innerStream.ReadAsync(_buffer, _offset, _buffer.Length);
            }

            return _count;
        }

        public async Task<int> EnsureBufferAsync(int min)
        {
            if (_count < min)
            {
                if (_buffer.Length < min)
                {
                    // Grow buffer
                    byte[] newBuffer = new byte[min];
                    if (_count > 0)
                    {
                        // Copy existing data
                        Array.Copy(_buffer, _offset, newBuffer, 0, _count);
                        _offset = 0;
                    }
                    _buffer = newBuffer;
                }
                else if (min - _count > _buffer.Length - (_offset + _count))
                {
                    // Shift down to make room
                    Array.Copy(_buffer, _offset, _buffer, 0, _count);
                    _offset = 0;
                }
                int read = 0;
                do
                {
                    // Fill buffer
                    read = await _innerStream.ReadAsync(_buffer, _offset + _count, _buffer.Length - (_offset + _count));
                    _count += read;
                } while (read != 0 && _count < min);
            }

            return _count;
        }

        public async Task<string> ReadLineAsync(int maxLength, Encoding encoding, char? termChar)
        {
            var maxLengthSpecified = maxLength > 0;
            int i;
            byte b = 0, b0;
            var read = false;
            using (var mem = new MemoryStream())
            {
                while (await EnsureBufferAsync() > 0)
                {
                    b0 = b;
                    i = ReadByte();
                    if (i == -1) break;
                    else read = true;

                    b = (byte)i;
                    if (maxLengthSpecified) maxLength--;

                    if (maxLengthSpecified && mem.Length == 1 && b == termChar && b0 == termChar)
                    {
                        maxLength++;
                        continue;
                    }

                    if (b == 10 || b == 13)
                    {
                        if (mem.Length == 0 && b == 10)
                        {
                            continue;
                        }
                        else break;
                    }

                    mem.WriteByte(b);
                    if (maxLengthSpecified && maxLength == 0)
                        break;
                }

                if (mem.Length == 0 && !read) return null;
                byte[] bytes = mem.ToArray();
                return encoding.GetString(bytes, 0, bytes.Length);
            }
        }

        public override void WriteByte(byte value)
        {
            _innerStream.WriteByte(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _innerStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            _innerStream.EndWrite(asyncResult);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            return _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void Flush()
        {
            _innerStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _innerStream.FlushAsync(cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
