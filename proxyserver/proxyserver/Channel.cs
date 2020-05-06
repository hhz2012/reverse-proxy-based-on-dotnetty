using DotNetty.Transport.Channels;
using HttpServer;
using System;
using System.Collections.Generic;
using System.Text;

namespace proxyserver
{
    public class Channel
    {
        public bool Busy { get; set; }
        public IChannel upstream { get; set; }
        public IChannelHandlerContext request { get; set; }
        public RequestHandler Handler { get; set; }
    }
}
