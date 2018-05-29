using System;
using System.Linq;
using System.Text;
using Agebull.Common.Logging;
using Agebull.ZeroNet.PubSub;
using Agebull.ZeroNet.ZeroApi;
using Newtonsoft.Json;
using ZeroMQ;

namespace Agebull.ZeroNet.Core
{
    /// <summary>
    ///     Zmq帮助类
    /// </summary>
    public static class ZeroHelper
    {
        #region Socket支持

        /// <summary>
        ///     关闭套接字
        /// </summary>
        public static void CloseSocket(this ZSocket socket)
        {
            if (socket == null)
                return;
            foreach (var con in socket.Connects.ToArray())
            {
                if (!socket.Disconnect(con, out var error))
                {
                    StationConsole.WriteError("CloseSocket", $"{error.Text}! Address:{socket.Connects.LinkToString(',')}.");
                }
            }

            foreach (var bin in socket.Binds.ToArray())
            {
                if (!socket.Unbind(bin, out var error))
                {
                    StationConsole.WriteError("CloseSocket",
                        $"{error.Text}! Address:{socket.Binds.LinkToString(',')}.");
                }
            }

            socket.Close();
            socket.Dispose();
        }

        /// <summary>
        /// 构建套接字
        /// </summary>
        /// <param name="address"></param>
        /// <param name="type"></param>
        /// <param name="identity"></param>
        /// <param name="subscribe"></param>
        /// <returns></returns>
        public static ZSocket CreateServiceSocket(string address, ZSocketType type, byte[] identity, string subscribe = null)
        {
            var socket = ZSocket.Create(type, out var error);
            if (error != null)
            {
                StationConsole.WriteError("CreateSocket", error.Text, $"Address:{address} type:{type}.");
                return null;
            }
            socket.SetOption(ZSocketOption.IDENTITY, identity);
            socket.SetOption(ZSocketOption.RECONNECT_IVL, 10);
            socket.SetOption(ZSocketOption.RECONNECT_IVL_MAX, 500);
            socket.SetOption(ZSocketOption.LINGER, 200);
            socket.SetOption(ZSocketOption.RCVTIMEO, 5000);
            socket.SetOption(ZSocketOption.BACKLOG, 10000);
            socket.SetOption(ZSocketOption.RCVHWM, 4096);
            //socket.Options.TcpKeepalive = true;
            //socket.Options.TcpKeepaliveIdle = new TimeSpan(0, 0, 10);
            //socket.Options.TcpKeepaliveInterval = new TimeSpan(0, 0, 0, 30);
            if (type == ZSocketType.SUB)
            {
                socket.SetOption(ZSocketOption.SUBSCRIBE, subscribe ?? "");
            }
            else
            {
                socket.SetOption(ZSocketOption.SNDTIMEO, 500);
                socket.SetOption(ZSocketOption.SNDHWM, 4096);
            }

            if (socket.Bind(address, out error))
                return socket;
            StationConsole.WriteError("CreateSocket", error.Text, $"address:{address} type:{type}.");
            socket.Close();
            socket.Dispose();
            return null;
        }
        /// <summary>
        /// 构建套接字
        /// </summary>
        /// <param name="address"></param>
        /// <param name="type"></param>
        /// <param name="identity"></param>
        /// <param name="subscribe"></param>
        /// <returns></returns>
        internal static ZSocket CreateClientSocket(string address, ZSocketType type, byte[] identity, string subscribe = null)
        {
            var socket = ZSocket.Create(type, out var error);
            if (error != null)
            {
                StationConsole.WriteError("CreateSocket", error.Text, $"Address:{address} type:{type}.");
                return null;
            }
            socket.SetOption(ZSocketOption.IDENTITY, identity);
            socket.SetOption(ZSocketOption.RECONNECT_IVL, 10);
            socket.SetOption(ZSocketOption.RECONNECT_IVL_MAX, 500);
            socket.SetOption(ZSocketOption.LINGER, 1000);
            socket.SetOption(ZSocketOption.RCVTIMEO, 5000);
            socket.SetOption(ZSocketOption.RCVHWM, 4096);
            socket.SetOption(ZSocketOption.SNDHWM, 4096);
            //socket.Options.TcpKeepalive = true;
            //socket.Options.TcpKeepaliveIdle = new TimeSpan(0, 0, 10);
            //socket.Options.TcpKeepaliveInterval = new TimeSpan(0, 0, 0, 30);
            if (type == ZSocketType.SUB)
            {
                socket.SetOption(ZSocketOption.SUBSCRIBE, subscribe ?? "");
            }
            else
            {
                socket.SetOption(ZSocketOption.SNDTIMEO, 500);
            }

            if (socket.Connect(address, out error))
                return socket;
            StationConsole.WriteError("CreateSocket", error.Text, $"address:{address} type:{type}.");
            socket.Close();
            socket.Dispose();
            return null;
        }

        /// <summary>
        /// 构建套接字
        /// </summary>
        /// <param name="address"></param>
        /// <param name="identity"></param>
        /// <returns></returns>
        public static ZSocket CreateRequestSocket(string address, byte[] identity)
        {
            return CreateClientSocket(address, ZSocketType.REQ, identity);
        }

        /// <summary>
        /// 构建套接字
        /// </summary>
        /// <param name="address"></param>
        /// <param name="identity"></param>
        /// <returns></returns>
        public static ZSocket CreateDealerSocket(string address, byte[] identity)
        {
            return CreateClientSocket(address, ZSocketType.DEALER, identity);
        }


        /// <summary>
        /// 构建套接字
        /// </summary>
        /// <param name="address"></param>
        /// <param name="identity"></param>
        /// <param name="subscribe"></param>
        /// <returns></returns>
        public static ZSocket CreateSubscriberSocket(string address, byte[] identity, string subscribe)
        {
            return CreateClientSocket(address, ZSocketType.SUB, identity, subscribe);
        }

        #endregion

        #region 调用支持

        /// <summary>
        ///     一次请求
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="desicription"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static ZeroResultData<string> QuietSend(this ZSocket socket, byte[] desicription, params string[] args)
        {
            using (var frames = new ZMessage
            {
                new ZFrame(desicription)
            })
            {

                if (args != null)
                {
                    foreach (var arg in args)
                    {
                        frames.Add(new ZFrame((arg ?? "").ToUtf8Bytes()));
                    }
                }

                try
                {
                    if (!socket.Send(frames, out var error))
                    {
                        return new ZeroResultData<string>
                        {
                            State = ZeroOperatorStateType.LocalRecvError,
                            ZmqErrorCode = error.Number,
                            ZmqErrorMessage = error.Text
                        };
                    }
                }
                catch (Exception e)
                {
                    LogRecorder.Exception(e);
                    return new ZeroResultData<string>
                    {
                        State = ZeroOperatorStateType.Exception,
                        Exception = e
                    };
                }
            }

            return new ZeroResultData<string>
            {
                State = ZeroOperatorStateType.Ok,
                InteractiveSuccess = true
            };
        }
        /// <summary>
        ///     一次请求
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="desicription"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static ZeroResultData<string> Send(this ZSocket socket, byte[] desicription, params string[] args)
        {
            using (var frames = new ZMessage
            {
                new ZFrame(desicription)
            })
            {

                if (args != null)
                {
                    foreach (var arg in args)
                    {
                        frames.Add(new ZFrame((arg ?? "").ToUtf8Bytes()));
                    }
                }

                try
                {
                    if (!socket.Send(frames, out var error))
                    {
                        StationConsole.WriteError("Send", error.Text, socket.Connects.LinkToString(','),
                            $"Socket Ptr:{socket.SocketPtr}");
                        return new ZeroResultData<string>
                        {
                            State = ZeroOperatorStateType.LocalRecvError,
                            ZmqErrorCode = error.Number,
                            ZmqErrorMessage = error.Text
                        };
                    }
                }
                catch (Exception e)
                {
                    StationConsole.WriteError("Send", "Exception", socket.Connects.LinkToString(','),
                        $"Socket Ptr:{socket.SocketPtr}", e);
                    LogRecorder.Exception(e);
                    return new ZeroResultData<string>
                    {
                        State = ZeroOperatorStateType.Exception,
                        Exception = e
                    };
                }
            }

            return new ZeroResultData<string>
            {
                State = ZeroOperatorStateType.Ok,
                InteractiveSuccess = true
            };
        }

        /// <summary>
        ///     一次请求
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="desicription"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static ZeroResultData<string> Call(this ZSocket socket, byte[] desicription, params string[] args)
        {
            var result = Send(socket, desicription, args);
            return !result.InteractiveSuccess ? result : socket.ReceiveString();
        }

        #endregion

        #region 广播支持

        /// <summary>
        ///     订阅时的标准网络数据说明
        /// </summary>
        public static readonly byte[] PubDescription =
        {
            5,
            ZeroByteCommand.General,
            ZeroFrameType.PubTitle,
            ZeroFrameType.RequestId,
            ZeroFrameType.Publisher,
            ZeroFrameType.SubTitle,
            ZeroFrameType.Argument,
            ZeroFrameType.End
        };

        /// <summary>
        ///     发送广播
        /// </summary>
        /// <param name="content"></param>
        /// <param name="socket"></param>
        /// <returns></returns>
        public static bool Publish<T>(this ZSocket socket, T content)
            where T : class, IPublishData
        {
            return Publish(socket, content.Title, null, JsonConvert.SerializeObject(content));
        }

        /// <summary>
        ///     发送广播
        /// </summary>
        /// <param name="title"></param>
        /// <param name="subTitle"></param>
        /// <param name="content"></param>
        /// <param name="socket"></param>
        /// <returns></returns>
        public static bool Publish<T>(this ZSocket socket, string title, string subTitle, T content)
            where T : class
        {
            return Publish(socket, title, subTitle, JsonConvert.SerializeObject(content));
        }

        /// <summary>
        ///     发送广播
        /// </summary>
        /// <param name="title"></param>
        /// <param name="content"></param>
        /// <param name="socket"></param>
        /// <returns></returns>
        public static bool Publish<T>(this ZSocket socket, string title, T content) where T : class
        {
            return Publish(socket, title, null, JsonConvert.SerializeObject(content));
        }

        /// <summary>
        ///     发送广播
        /// </summary>
        /// <param name="item"></param>
        /// <param name="socket"></param>
        /// <returns></returns>
        public static bool Publish(this ZSocket socket, PublishItem item)
        {
            return Publish(socket, item.Title, item.SubTitle, item.Content);
        }

        /// <summary>
        ///     发送广播
        /// </summary>
        /// <param name="title"></param>
        /// <param name="subTitle"></param>
        /// <param name="content"></param>
        /// <param name="socket"></param>
        /// <returns></returns>
        public static bool Publish(this ZSocket socket, string title, string subTitle, string content)
        {

            try
            {
                using (var frames = new ZMessage
                {
                    new ZFrame(PubDescription),
                    new ZFrame((title ?? "").ToUtf8Bytes()),
                    new ZFrame(ApiContext.RequestContext.RequestId.ToUtf8Bytes()),
                    new ZFrame(ZeroApplication.Config.RealName.ToUtf8Bytes()),
                    new ZFrame((subTitle ?? "").ToUtf8Bytes()),
                    new ZFrame((content ?? "").ToUtf8Bytes())
                })
                {
                    if (!socket.Send(frames, out var error))
                    {
                        StationConsole.WriteError("Pub", error.Text,
                            $"{title}:{subTitle} =>{socket.Connects.LinkToString(',')}", $"Socket Ptr:{socket.SocketPtr}");
                        return false;
                    }
                }

                var result = socket.ReceiveString();
                return result.InteractiveSuccess && result.State == ZeroOperatorStateType.Ok;
            }
            catch (Exception e)
            {
                StationConsole.WriteError("Pub", "Exception", socket.Connects.LinkToString(','), $"Socket Ptr:{socket.SocketPtr}", e);
                LogRecorder.Exception(e);
                return false;
            }
        }

        /// <summary>
        ///     接收广播
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="item"></param>
        /// <param name="showError"></param>
        /// <returns></returns>
        public static bool Subscribe(this ZSocket socket, out PublishItem item, bool showError = true)
        {
            ZMessage messages;
            try
            {
                messages = socket.ReceiveMessage(out var error);
                if (error != null)
                {
                    if (error.Number != 11 && showError)
                        StationConsole.WriteError("Sub", error.Text, socket.Connects.LinkToString(','), $"Socket Ptr:{socket.SocketPtr}");
                    item = null;
                    return false;
                }
            }
            catch (Exception e)
            {
                StationConsole.WriteError("Sub", "Exception", socket.Connects.LinkToString(','), $"Socket Ptr:{socket.SocketPtr}", e);
                LogRecorder.Exception(e);
                item = null;
                return false;
            }

            try
            {
                if (messages.Count < 3)
                {
                    item = null;
                    return false;
                }
                var description = messages[1].Read();
                if (description.Length < 2)
                {
                    item = null;
                    return false;
                }

                int end = description[0] + 2;
                if (end != messages.Count)
                {
                    item = null;
                    return false;
                }

                item = new PublishItem
                {
                    Title = messages[0].ReadString()
                };

                for (int idx = 2; idx < end; idx++)
                {
                    var bytes = messages[idx].Read();
                    if (bytes.Length == 0)
                        continue;
                    var val = Encoding.UTF8.GetString(bytes);
                    switch (description[idx])
                    {
                        case ZeroFrameType.SubTitle:
                            item.SubTitle = val;
                            break;
                        case ZeroFrameType.Publisher:
                            item.Station = val;
                            break;
                        case ZeroFrameType.Argument:
                            item.Content = val;
                            break;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                StationConsole.WriteError("Sub", "Exception", socket.Connects.LinkToString(','), $"Socket Ptr:{socket.SocketPtr}", e);
                LogRecorder.Exception(e);
                item = null;
                return false;
            }
            finally
            {
                messages.Dispose();
            }
        }

        #endregion

        #region 接收支持

        /// <summary>
        ///     接收文本
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="error"></param>
        /// <param name="tryCnt">重试次数（默认10次，即允许最大5秒钟的超时）</param>
        /// <param name="showError">是否显示错误</param>
        /// <returns></returns>
        public static ZeroResultData<string> ReceiveString(this ZSocket socket, out ZError error, int tryCnt = 10, bool showError = true)
        {
            tryCnt = 0;
            ZMessage messages;
            int doCnt = 0;
            try
            {
                while (true)
                {
                    messages = socket.ReceiveMessage(out error);
                    if (error == null)
                    {
                        if (doCnt > 0 && showError)
                            StationConsole.WriteInfo("Receive", socket.Connects.LinkToString(','), "Slow", $"ReTry:{doCnt}.Socket Ptr:{ socket.SocketPtr}");
                        break;
                    }
                    if (++doCnt < tryCnt && Equals(error, ZError.EAGAIN))
                        continue;
                    if (showError)
                        StationConsole.WriteError("Receive", socket.Connects.LinkToString(','), error.Text, $"ReTry:{doCnt}.Socket Ptr:{ socket.SocketPtr}");
                    return new ZeroResultData<string>
                    {
                        State = ZeroOperatorStateType.LocalRecvError,
                        ZmqErrorMessage = error.Text,
                        ZmqErrorCode = error.Number
                    };
                }
            }
            catch (Exception e)
            {
                error = null;
                if (showError)
                    StationConsole.WriteError("Receive", "Exception", socket.Connects.LinkToString(','), $"ReTry:{doCnt}.Socket Ptr:{ socket.SocketPtr}.", e);
                LogRecorder.Exception(e);
                return new ZeroResultData<string>
                {
                    State = ZeroOperatorStateType.Exception,
                    Exception = e
                };
            }

            try
            {
                var description = messages[0].Read();
                if (description.Length == 0)
                {
                    if (showError)
                        StationConsole.WriteError("Receive", "LaoutError", socket.Connects.LinkToString(','), description.LinkToString(p => p.ToString("X2"), ""), $"Socket Ptr:{ socket.SocketPtr}.");
                    return new ZeroResultData<string>
                    {
                        State = ZeroOperatorStateType.Invalid,
                        ZmqErrorMessage = "网络格式错误",
                        ZmqErrorCode = -1
                    };
                }

                int end = description[0] + 1;
                if (end != messages.Count)
                {
                    if (showError)
                        StationConsole.WriteError("Receive", "LaoutError", socket.Connects.LinkToString(','), $"FrameSize{messages.Count}", description.LinkToString(p => p.ToString("X2"), ""), $"Socket Ptr:{ socket.SocketPtr}.");
                    return new ZeroResultData<string>
                    {
                        State = ZeroOperatorStateType.Invalid,
                        ZmqErrorMessage = "网络格式错误",
                        ZmqErrorCode = -2
                    };
                }

                var result = new ZeroResultData<string>
                {
                    InteractiveSuccess = true,
                    State = (ZeroOperatorStateType)description[1]
                };
                for (int idx = 1; idx < end; idx++)
                {
                    result.Add(description[idx + 1], Encoding.UTF8.GetString(messages[idx].Read()));
                }

                return result;
            }
            catch (Exception e)
            {
                LogRecorder.Exception(e);
                if (showError)
                    StationConsole.WriteError("Receive", "Exception", socket.Connects.LinkToString(','), $"Socket Ptr:{ socket.SocketPtr}.", e);
                return new ZeroResultData<string>
                {
                    State = ZeroOperatorStateType.Exception,
                    Exception = e
                };
            }
            finally
            {
                messages.Dispose();
            }
        }

        /// <summary>
        ///     接收文本
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="tryCnt">重试次数（默认10次，即允许最大5秒钟的超时）</param>
        /// <param name="showError">是否显示错误</param>
        /// <returns></returns>
        public static ZeroResultData<string> ReceiveString(this ZSocket socket, int tryCnt = 10, bool showError = true)
        {
            return ReceiveString(socket, out _, tryCnt, showError);
        }

        /// <summary>
        ///     接收字节
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="tryCnt"></param>
        /// <returns></returns>
        public static ZeroResultData<byte[]> Receive(this ZSocket socket, int tryCnt = 10)
        {
            tryCnt = 0;
            ZMessage messages;
            int doCnt = 0;
            try
            {
                while (true)
                {
                    messages = socket.ReceiveMessage(out var error);
                    if (error == null)
                    {
                        if (doCnt > 0)
                            StationConsole.WriteInfo("Receive", socket.Connects.LinkToString(','), $"ReTry:{doCnt}.Socket Ptr:{ socket.SocketPtr}");
                        break;
                    }
                    if (++doCnt < tryCnt)
                        continue;
                    StationConsole.WriteError("Receive", socket.Connects.LinkToString(','), error.Text, $"ReTry:{doCnt}.Socket Ptr:{ socket.SocketPtr}.");
                    return new ZeroResultData<byte[]>
                    {
                        State = ZeroOperatorStateType.LocalRecvError,
                        ZmqErrorMessage = error.Text,
                        ZmqErrorCode = error.Number
                    };
                }
            }
            catch (Exception e)
            {
                StationConsole.WriteError("Receive", socket.Connects.LinkToString(','), "Exception", $"ReTry:{doCnt}.Socket Ptr:{ socket.SocketPtr}.", e);
                LogRecorder.Exception(e);
                return new ZeroResultData<byte[]>
                {
                    State = ZeroOperatorStateType.Exception,
                    Exception = e
                };
            }

            try
            {
                var description = messages[0].Read();
                if (description.Length < 2)
                {
                    StationConsole.WriteError("Receive", "LaoutError", socket.Connects.LinkToString(','), description.LinkToString(p => p.ToString("X2"), ""), $"Socket Ptr:{ socket.SocketPtr}.");
                    return new ZeroResultData<byte[]>
                    {
                        State = ZeroOperatorStateType.Invalid,
                        ZmqErrorMessage = "网络格式错误",
                        ZmqErrorCode = -1
                    };
                }

                int end = description[0] + 1;
                if (end != messages.Count)
                {
                    StationConsole.WriteError("Receive", "LaoutError", socket.Connects.LinkToString(','), $"FrameSize{messages.Count}", description.LinkToString(p => p.ToString("X2"), ""), $"Socket Ptr:{ socket.SocketPtr}.");
                    return new ZeroResultData<byte[]>
                    {
                        State = ZeroOperatorStateType.Invalid,
                        ZmqErrorMessage = "网络格式错误",
                        ZmqErrorCode = -2
                    };
                }

                var result = new ZeroResultData<byte[]>
                {
                    InteractiveSuccess = true,
                    State = (ZeroOperatorStateType)description[1]
                };
                for (int idx = 1; idx < end; idx++)
                {
                    result.Add(description[idx + 1], messages[idx].Read());
                }

                return result;
            }
            catch (Exception e)
            {
                StationConsole.WriteError("Receive", "Exception", socket.Connects.LinkToString(','), $"FrameSize{messages.Count},Socket Ptr:{ socket.SocketPtr}.");
                LogRecorder.Exception(e);
                return new ZeroResultData<byte[]>
                {
                    State = ZeroOperatorStateType.Exception,
                    Exception = e
                };
            }
            finally
            {
                messages.Dispose();
            }
        }

        /// <summary>
        ///     接收文本
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="tryCnt"></param>
        /// <returns></returns>
        public static ZeroResultData<byte[]> ReceiveUnknow(this ZSocket socket, int tryCnt = 3)
        {
            tryCnt = 0;
            ZMessage messages;
            int doCnt = 0;
            try
            {
                while (true)
                {
                    messages = socket.ReceiveMessage(out var error);
                    if (error == null)
                        break;
                    if (error.Number == 11 && ++doCnt < tryCnt)
                        continue;
                    StationConsole.WriteError("Receive", socket.Connects.LinkToString(','), error.Text, $"ReTry:{doCnt}.Socket Ptr:{ socket.SocketPtr}.");
                    return new ZeroResultData<byte[]>
                    {
                        State = ZeroOperatorStateType.LocalRecvError,
                        ZmqErrorMessage = error.Text,
                        ZmqErrorCode = error.Number
                    };
                }
            }
            catch (Exception e)
            {
                StationConsole.WriteError("Receive", socket.Connects.LinkToString(','), "Exception", $"ReTry:{doCnt}., Socket Ptr:{ socket.SocketPtr}.", e);
                LogRecorder.Exception(e);
                return new ZeroResultData<byte[]>
                {
                    State = ZeroOperatorStateType.Exception,
                    Exception = e
                };
            }

            try
            {
                var result = new ZeroResultData<byte[]>
                {
                    InteractiveSuccess = true,
                    State = ZeroOperatorStateType.Ok
                };
                foreach (var frame in messages)
                {
                    result.Add(ZeroFrameType.BinaryValue, frame.Read());
                }

                return result;
            }
            catch (Exception e)
            {
                StationConsole.WriteError("Receive", "Exception", socket.Connects.LinkToString(','), $"FrameSize{messages.Count}, Socket Ptr:{ socket.SocketPtr}.");
                LogRecorder.Exception(e);
                return new ZeroResultData<byte[]>
                {
                    State = ZeroOperatorStateType.Exception,
                    Exception = e
                };
            }
            finally
            {
                messages.Dispose();
            }
        }

        #endregion

    }
}