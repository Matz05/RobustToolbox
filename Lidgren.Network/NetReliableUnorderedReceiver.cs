﻿using System;

namespace Lidgren.Network
{
	internal sealed class NetReliableUnorderedReceiver : NetReceiverChannelBase
	{
		private int m_windowStart;
		private int m_windowSize;
		private NetBitVector m_earlyReceived;

		public NetReliableUnorderedReceiver(NetConnection connection, int windowSize)
			: base(connection)
		{
			m_windowSize = windowSize;
			m_earlyReceived = new NetBitVector(windowSize);
		}

		private void AdvanceWindow()
		{
			m_earlyReceived.Set(m_windowStart % m_windowSize, false);
			m_windowStart = (m_windowStart + 1) % NetConstants.NumSequenceNumbers;
		}

		internal override void ReceiveMessage(NetIncomingMessage message)
		{
			int relate = NetUtility.RelativeSequenceNumber(message.m_sequenceNumber, m_windowStart);

			// ack no matter what
			m_connection.QueueAck(message.m_receivedMessageType, message.m_sequenceNumber);

			if (relate == 0)
			{
				AdvanceWindow();
				m_peer.ReleaseMessage(message);

				// release withheld messages
				int nextSeqNr = (message.m_sequenceNumber + 1) % NetConstants.NumSequenceNumbers;

				while (m_earlyReceived[nextSeqNr % m_windowSize])
				{
					AdvanceWindow();
					nextSeqNr++;
				}

				return;
			}

			if (relate < 0)
			{
				// duplicate
				m_peer.LogVerbose("Received message #" + message.m_sequenceNumber + " DROPPING DUPLICATE");
				return;
			}

			// relate > 0 = early message
			if (relate > m_windowSize)
			{
				// too early message!
				m_peer.LogDebug("Received " + message + " TOO EARLY! Expected " + m_windowStart);
				return;
			}

			m_earlyReceived.Set(message.m_sequenceNumber % m_windowSize, true);

			m_peer.ReleaseMessage(message);
		}
	}
}