// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace HttpServer
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using System;
    using DotNetty.Common;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using DotNetty.Transport.Bootstrapping;
    using System.Net;
    using System.Threading.Tasks;
    using System.Collections.Concurrent;
    using proxyserver;

    public class RequestHandler : ChannelHandlerAdapter
    {
        static readonly ThreadLocalCache Cache = new ThreadLocalCache();

        sealed class ThreadLocalCache : FastThreadLocal<AsciiString>
        {
            protected override AsciiString GetInitialValue()
            {
                DateTime dateTime = DateTime.UtcNow;
                return AsciiString.Cached($"{dateTime.DayOfWeek}, {dateTime:dd MMM yyyy HH:mm:ss z}");
            }
        }

        public static readonly byte[] StaticPlaintext = Encoding.UTF8.GetBytes("Hello, World!");
        public static readonly int StaticPlaintextLen = StaticPlaintext.Length;
        public static readonly IByteBuffer PlaintextContentBuffer = Unpooled.UnreleasableBuffer(Unpooled.DirectBuffer().WriteBytes(StaticPlaintext));
        public static readonly AsciiString PlaintextClheaderValue = AsciiString.Cached($"{StaticPlaintextLen}");
        static readonly AsciiString JsonClheaderValue = AsciiString.Cached($"{JsonLen()}");

        public static readonly AsciiString TypePlain = AsciiString.Cached("text/plain");
        static readonly AsciiString TypeJson = AsciiString.Cached("application/json");
        static readonly AsciiString ServerName = AsciiString.Cached("Netty");
        static readonly AsciiString ContentTypeEntity = HttpHeaderNames.ContentType;
        static readonly AsciiString DateEntity = HttpHeaderNames.Date;
        static readonly AsciiString ContentLengthEntity = HttpHeaderNames.ContentLength;
        static readonly AsciiString ServerEntity = HttpHeaderNames.Server;

        volatile ICharSequence date = Cache.Value;

        static int JsonLen() => Encoding.UTF8.GetBytes(NewMessage().ToJsonFormat()).Length;

        static MessageBody NewMessage() => new MessageBody("Hello, World!");

        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            if (message is IHttpRequest request)
            {
                try
                {
                    this.Process(ctx, request);
                }
                finally
                {
                    ReferenceCountUtil.Release(message);
                }
            }
            else
            {
                ctx.FireChannelRead(message);
            }
        }

        public void Response(IChannelHandlerContext ctx)
        {
            this.WriteResponse(ctx, PlaintextContentBuffer.Duplicate(), TypePlain, PlaintextClheaderValue);
        }
        void Process(IChannelHandlerContext ctx, IHttpRequest request)
        {
            string uri = request.Uri;
          
            Task.Run(async () =>
            {
                HandleProxy.AddRequest(ctx, request, this);
            }
             )
                ;

        }
        public static List<IChannelHandlerContext> requests = new List<IChannelHandlerContext>();

        void WriteResponse(IChannelHandlerContext ctx, IByteBuffer buf, ICharSequence contentType, ICharSequence contentLength)
        {
            // Build the response object.
            var response = new DefaultFullHttpResponse(DotNetty.Codecs.Http.HttpVersion.Http11, HttpResponseStatus.OK, buf, false);
            HttpHeaders headers = response.Headers;
            headers.Set(ContentTypeEntity, contentType);
            headers.Set(ServerEntity, ServerName);
            headers.Set(DateEntity, this.date);
            headers.Set(ContentLengthEntity, contentLength);

            // Close the non-keep-alive connection after the write operation is done.
            ctx.WriteAsync(response);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => context.CloseAsync();

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();
    }
   
   

}
