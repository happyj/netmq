﻿using System;
using JetBrains.Annotations;
using NetMQ.zmq;
using NetMQ.zmq.Utils;

namespace NetMQ
{
    /// <summary>
    /// Abstract base class for NetMQ's different socket types.
    /// </summary>
    /// <remarks>
    /// Various options are available in this base class, though their affect can vary by socket type.
    /// </remarks>
    public abstract class NetMQSocket : IOutgoingSocket, IReceivingSocket, ISocketPollable, IDisposable
    {
        private readonly SocketBase m_socketHandle;
        private bool m_isClosed;
        private readonly NetMQSocketEventArgs m_socketEventArgs;

        private EventHandler<NetMQSocketEventArgs> m_receiveReady;

        private EventHandler<NetMQSocketEventArgs> m_sendReady;

        private readonly Selector m_selector;

        /// <summary>
        /// Create a new NetMQSocket with the given <see cref="SocketBase"/>.
        /// </summary>
        /// <param name="socketHandle">a SocketBase object to assign to the new socket</param>
        internal NetMQSocket([NotNull] SocketBase socketHandle)
        {
            m_selector = new Selector();
            m_socketHandle = socketHandle;
            Options = new SocketOptions(this);
            m_socketEventArgs = new NetMQSocketEventArgs(this);
        }

        /// <summary>
        /// This event occurs when at least one message may be received from the socket without blocking.
        /// </summary>
        /// <remarks>
        /// This event is raised when a <see cref="NetMQSocket"/> is added to a running <see cref="Poller"/>.
        /// </remarks>
        public event EventHandler<NetMQSocketEventArgs> ReceiveReady
        {
            add
            {
                m_receiveReady += value;
                InvokeEventsChanged();
            }
            remove
            {
                m_receiveReady -= value;
                InvokeEventsChanged();
            }
        }

        /// <summary>
        /// This event occurs when at least one message may be sent via the socket without blocking.
        /// </summary>
        /// <remarks>
        /// This event is raised when a <see cref="NetMQSocket"/> is added to a running <see cref="Poller"/>.
        /// </remarks>
        public event EventHandler<NetMQSocketEventArgs> SendReady
        {
            add
            {
                m_sendReady += value;
                InvokeEventsChanged();
            }
            remove
            {
                m_sendReady -= value;
                InvokeEventsChanged();
            }
        }

        /// <summary>
        /// Fires when either the <see cref="SendReady"/> or <see cref="ReceiveReady"/> event is set.
        /// </summary>
        internal event EventHandler<NetMQSocketEventArgs> EventsChanged;

        /// <summary>
        /// Get or set an integer that represents the number of errors that have accumulated.
        /// </summary>
        internal int Errors { get; set; }

        /// <summary>
        /// Raise the <see cref="EventsChanged"/> event.
        /// </summary>
        private void InvokeEventsChanged()
        {
            var temp = EventsChanged;

            if (temp != null)
            {
                m_socketEventArgs.Init(PollEvents.None);
                temp(this, m_socketEventArgs);
            }
        }

        /// <summary>
        /// Get the <see cref="SocketOptions"/> of this socket.
        /// </summary>
        public SocketOptions Options { get; private set; }

        /// <summary>
        /// Get the underlying <see cref="SocketBase"/>.
        /// </summary>
        internal SocketBase SocketHandle
        {
            get { return m_socketHandle; }
        }

        NetMQSocket ISocketPollable.Socket
        {
            get { return this; }
        }

        /// <summary>
        /// Bind the socket to <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string representing the address to bind this socket to</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        /// <exception cref="NetMQException">No IO thread was found, or the protocol's listener encountered an
        /// error during initialisation.</exception>
        public void Bind([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.Bind(address);
        }

        /// <summary>Binds the specified TCP <paramref name="address"/> to an available port, assigned by the operating system.</summary>
        /// <returns>the chosen port-number</returns>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="ProtocolNotSupportedException"><paramref name="address"/> uses a protocol other than TCP.</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        /// <exception cref="NetMQException">No IO thread was found, or the protocol's listener errored during
        /// initialisation.</exception>
        public int BindRandomPort([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            return m_socketHandle.BindRandomPort(address);
        }

        /// <summary>
        /// Connect the socket to <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string denoting the address to connect this socket to</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="NetMQException">No IO thread was found.</exception>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        public void Connect([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.Connect(address);
        }

        /// <summary>
        /// Disconnect this socket from <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string denoting the address to disconnect from</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="EndpointNotFoundException">Endpoint was not found and cannot be disconnected.</exception>
        public void Disconnect([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.TermEndpoint(address);
        }

        /// <summary>
        /// Unbind this socket from <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string denoting the address to unbind from</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="EndpointNotFoundException">Endpoint was not found and cannot be disconnected.</exception>
        public void Unbind([NotNull] string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.TermEndpoint(address);
        }

        /// <summary>Closes this socket, rendering it unusable. Equivalent to calling <see cref="Dispose"/>.</summary>
        public void Close()
        {
            if (m_isClosed)
                return;

            m_isClosed = true;

            m_socketHandle.CheckDisposed();
            m_socketHandle.Close();
        }

        #region Polling

        /// <summary>
        /// Wait until a message is ready to be received from the socket.
        /// </summary>
        public void Poll()
        {
            Poll(TimeSpan.FromMilliseconds(-1));
        }

        /// <summary>
        /// Wait until a message is ready to be received/sent from this socket or until timeout is reached.
        /// If a message is available, the ReceiveReady/SendReady event is fired.
        /// </summary>
        /// <param name="timeout">a TimeSpan that represents the timeout-period</param>
        /// <returns>true if a message was available within the timeout, false otherwise</returns>
        public bool Poll(TimeSpan timeout)
        {
            PollEvents events = GetPollEvents();

            var result = Poll(events, timeout);

            InvokeEvents(this, result);

            return result != PollEvents.None;
        }

        /// <summary>
        /// Poll this socket, which means wait for an event to happen within the given timeout period.
        /// </summary>
        /// <param name="pollEvents">the poll event(s) to listen for</param>
        /// <param name="timeout">the timeout period</param>
        /// <returns>
        /// PollEvents.None     -> no message available
        /// PollEvents.PollIn   -> no message arrived
        /// PollEvents.PollOut  -> no message to send
        /// PollEvents.Error    -> an error has occurred
        /// or any combination thereof
        /// </returns>
        /// <exception cref="FaultException">The internal select operation failed.</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        public PollEvents Poll(PollEvents pollEvents, TimeSpan timeout)
        {
            SelectItem[] items = { new SelectItem(SocketHandle, pollEvents) };

            m_selector.Select(items, 1, (int)timeout.TotalMilliseconds);
            return items[0].ResultEvent;
        }

        /// <summary>
        /// Return a <see cref="PollEvents"/> value that indicates which bit-flags have a corresponding listener,
        /// with PollError always set,
        /// and PollOut set based upon m_sendReady
        /// and PollIn set based upon m_receiveReady.
        /// </summary>
        /// <returns>a PollEvents value that denotes which events have a listener</returns>
        internal PollEvents GetPollEvents()
        {
            var events = PollEvents.PollError;

            if (m_sendReady != null)
            {
                events |= PollEvents.PollOut;
            }

            if (m_receiveReady != null)
            {
                events |= PollEvents.PollIn;
            }

            return events;
        }

        /// <summary>
        /// Unless this socket is closed,
        /// based upon the given PollEvents - raise the m_receiveReady event if PollIn is set,
        /// and m_sendReady if PollOut is set.
        /// </summary>
        /// <param name="sender">what to use as the source of the events</param>
        /// <param name="events">the given PollEvents that dictates when of the two events to raise</param>
        internal void InvokeEvents(object sender, PollEvents events)
        {
            if (!m_isClosed)
            {
                m_socketEventArgs.Init(events);

                if (events.HasFlag(PollEvents.PollIn))
                {
                    var temp = m_receiveReady;
                    if (temp != null)
                    {
                        temp(sender, m_socketEventArgs);
                    }
                }

                if (events.HasFlag(PollEvents.PollOut))
                {
                    var temp = m_sendReady;
                    if (temp != null)
                    {
                        temp(sender, m_socketEventArgs);
                    }
                }
            }
        }

        #endregion

        #region Receiving messages

        /// <summary>Receive the next message from this socket.</summary>
        /// <remarks>Whether the request blocks or not is controlled by <paramref name="options"/>.</remarks>
        /// <param name="msg">The Msg object to put it in</param>
        /// <param name="options">Either <see cref="SendReceiveOptions.None"/>, or <see cref="SendReceiveOptions.DontWait"/>.
        /// <see cref="SendReceiveOptions.SendMore"/> is ignored.</param>
        /// <exception cref="AgainException">The receive operation timed out.</exception>
        [Obsolete("Use Receive(ref Msg) or TryReceive(ref Msg,TimeSpan) instead.")]
        public virtual void Receive(ref Msg msg, SendReceiveOptions options)
        {
            // This legacy method adapts the newer nothrow API to the older AgainException one.

            if ((options & SendReceiveOptions.DontWait) != 0)
            {
                // User specified DontWait, so use a zero timeout.
                if (!m_socketHandle.TryRecv(ref msg, TimeSpan.Zero))
                    throw new AgainException();
            }
            else
            {
                // User is expecting to wait, however we must still consider the socket's (obsolete) ReceiveTimeout.
                if (!m_socketHandle.TryRecv(ref msg, Options.ReceiveTimeout))
                    throw new AgainException();
            }
        }

        /// <summary>Receive the next message from this socket, blocking indefinitely if necessary.</summary>
        /// <param name="msg">A reference to a <see cref="Msg"/> instance into which the received message
        /// data should be placed.</param>
        public virtual void Receive(ref Msg msg)
        {
            m_socketHandle.Recv(ref msg);
        }

        /// <summary>Attempt to receive a message for the specified amount of time.</summary>
        /// <param name="msg">A reference to a <see cref="Msg"/> instance into which the received message
        /// data should be placed.</param>
        /// <param name="timeout">The maximum amount of time the call should wait for a message before returning.</param>
        /// <returns><c>true</c> if a message was received before <paramref name="timeout"/> elapsed,
        /// otherwise <c>false</c>.</returns>
        public virtual bool TryReceive(ref Msg msg, TimeSpan timeout)
        {
            return m_socketHandle.TryRecv(ref msg, timeout);
        }

        #endregion

        #region Sending messages

        /// <summary>
        /// Send the given Msg out upon this socket.
        /// The message content is in the form of a byte-array that Msg contains.
        /// </summary>
        /// <param name="msg">the Msg struct that contains the data and the options for this transmission</param>
        /// <param name="options">a SendReceiveOptions value that can specify the DontWait or SendMore bits (or None)</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="FaultException"><paramref name="msg"/> is not initialised.</exception>
        /// <exception cref="AgainException">The send operation timed out.</exception>
        public virtual void Send(ref Msg msg, SendReceiveOptions options)
        {
            m_socketHandle.Send(ref msg, options);
        }

        #endregion

        #region Unsubscribe (obsolete)

        [Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
        public virtual void Subscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        [Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
        public virtual void Subscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Subscribe, topic);
        }

        [Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
        public virtual void Unsubscribe(string topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        [Obsolete("Do not use this method if the socket is different from Subscriber and XSubscriber")]
        public virtual void Unsubscribe(byte[] topic)
        {
            SetSocketOption(ZmqSocketOptions.Unsubscribe, topic);
        }

        #endregion

        /// <summary>
        /// Listen to the given endpoint for SocketEvent events.
        /// </summary>
        /// <param name="endpoint">A string denoting the endpoint to monitor</param>
        /// <param name="events">The specific <see cref="SocketEvent"/> events to report on. Defaults to <see cref="SocketEvent.All"/> if ommitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="endpoint"/> cannot be empty or whitespace.</exception>
        public void Monitor([NotNull] string endpoint, SocketEvent events = SocketEvent.All)
        {
            if (endpoint == null)
                throw new ArgumentNullException("endpoint");
            if (string.IsNullOrEmpty(endpoint))
                throw new ArgumentException("Cannot be empty.", "endpoint");

            m_socketHandle.CheckDisposed();

            m_socketHandle.Monitor(endpoint, events);
        }

        /// <summary>
        /// Get whether a message is waiting to be picked up (<c>true</c> if there is, <c>false</c> if there is none).
        /// </summary>
        public bool HasIn
        {
            get
            {
                var pollEvents = GetSocketOptionX<PollEvents>(ZmqSocketOptions.Events);

                return pollEvents.HasFlag(PollEvents.PollIn);
            }
        }

        /// <summary>
        /// Get whether a message is waiting to be sent.
        /// </summary>
        /// <remarks>
        /// This is <c>true</c> if at least one message is waiting to be sent, <c>false</c> if there is none.
        /// </remarks>
        public bool HasOut
        {
            get
            {
                var pollEvents = GetSocketOptionX<PollEvents>(ZmqSocketOptions.Events);

                return pollEvents.HasFlag(PollEvents.PollOut);
            }
        }

        #region Socket options

        /// <summary>
        /// Get the integer-value of the specified <see cref="ZmqSocketOptions"/>.
        /// </summary>
        /// <param name="socketOptions">a ZmqSocketOptions that specifies what to get</param>
        /// <returns>an integer that is the value of that option</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal int GetSocketOption(ZmqSocketOptions socketOptions)
        {
            m_socketHandle.CheckDisposed();

            return m_socketHandle.GetSocketOption(socketOptions);
        }

        /// <summary>
        /// Get the (generically-typed) value of the specified <see cref="ZmqSocketOptions"/>.
        /// </summary>
        /// <param name="socketOptions">a ZmqSocketOptions that specifies what to get</param>
        /// <returns>an object of the given type, that is the value of that option</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal T GetSocketOptionX<T>(ZmqSocketOptions socketOptions)
        {
            m_socketHandle.CheckDisposed();

            return (T)m_socketHandle.GetSocketOptionX(socketOptions);
        }

        /// <summary>
        /// Get the <see cref="TimeSpan"/> value of the specified ZmqSocketOptions.
        /// </summary>
        /// <param name="socketOptions">a ZmqSocketOptions that specifies what to get</param>
        /// <returns>a TimeSpan that is the value of that option</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal TimeSpan GetSocketOptionTimeSpan(ZmqSocketOptions socketOptions)
        {
            return TimeSpan.FromMilliseconds(GetSocketOption(socketOptions));
        }

        /// <summary>
        /// Get the 64-bit integer-value of the specified <see cref="ZmqSocketOptions"/>.
        /// </summary>
        /// <param name="socketOptions">a ZmqSocketOptions that specifies what to get</param>
        /// <returns>a long that is the value of that option</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal long GetSocketOptionLong(ZmqSocketOptions socketOptions)
        {
            return GetSocketOptionX<long>(socketOptions);
        }

        /// <summary>
        /// Assign the given integer value to the specified <see cref="ZmqSocketOptions"/>.
        /// </summary>
        /// <param name="socketOptions">a ZmqSocketOptions that specifies what to set</param>
        /// <param name="value">an integer that is the value to set that option to</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal void SetSocketOption(ZmqSocketOptions socketOptions, int value)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.SetSocketOption(socketOptions, value);
        }

        /// <summary>
        /// Assign the given TimeSpan to the specified <see cref="ZmqSocketOptions"/>.
        /// </summary>
        /// <param name="socketOptions">a ZmqSocketOptions that specifies what to set</param>
        /// <param name="value">a TimeSpan that is the value to set that option to</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal void SetSocketOptionTimeSpan(ZmqSocketOptions socketOptions, TimeSpan value)
        {
            SetSocketOption(socketOptions, (int)value.TotalMilliseconds);
        }

        /// <summary>
        /// Assign the given Object value to the specified <see cref="ZmqSocketOptions"/>.
        /// </summary>
        /// <param name="socketOptions">a ZmqSocketOptions that specifies what to set</param>
        /// <param name="value">an object that is the value to set that option to</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal void SetSocketOption(ZmqSocketOptions socketOptions, object value)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.SetSocketOption(socketOptions, value);
        }

        #endregion

        /// <summary>Closes this socket, rendering it unusable. Equivalent to calling <see cref="Close"/>.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            Close();
        }
    }
}
