using DotNetty.Codecs.Http;
using DotNetty.Transport.Channels;
using HttpServer;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace proxyserver
{
    public class TestEventChannelInboundHandlerAdapter : ChannelHandlerAdapter
    {
        public TestEventChannelInboundHandlerAdapter()
        {

        }
        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            IFullHttpMessage fm = message as IFullHttpMessage;
            try
            {
                Task.Run(async () =>
                {
                    HandleProxy.OnEnd(context, message);
                }
            );

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
