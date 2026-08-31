// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;

namespace AvenChart.Api.Features.Telehealth;

/// <summary>
/// Process-local relay for the local-only WebRTC POC. SDP and ICE material is
/// intentionally never persisted, audited, logged, or sent to an external service.
/// </summary>
public sealed class TelehealthLocalWebRtcPocRelay
{
    private const int MaximumSignalsPerSession = 256;
    private readonly ConcurrentDictionary<Guid, SessionSignals> _sessions = new();

    public int Append(Guid sessionId, string senderRole, string kind, string payload, DateTimeOffset expiresAt)
    {
        RemoveExpiredSessions();
        var session = _sessions.GetOrAdd(sessionId, static _ => new SessionSignals());
        lock (session.Gate)
        {
            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                _sessions.TryRemove(sessionId, out _);
                throw TelehealthProblem.NotFound();
            }

            session.Signals.RemoveAll(signal => signal.ExpiresAt <= DateTimeOffset.UtcNow);
            session.ExpiresAt = expiresAt;
            if (session.Signals.Count >= MaximumSignalsPerSession)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_local_webrtc_signal_limit_reached",
                    "The local media POC has too many pending negotiation messages. Stop and reconnect both participants.");
            }

            var sequence = checked(++session.NextSequence);
            session.Signals.Add(new Signal(sequence, senderRole, kind, payload, expiresAt));
            return sequence;
        }
    }

    public TelehealthLocalWebRtcSignalReadResponse Read(Guid sessionId, string recipientRole, int afterSequence, DateTimeOffset expiresAt)
    {
        RemoveExpiredSessions();
        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(sessionId, out _);
            throw TelehealthProblem.NotFound();
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return new TelehealthLocalWebRtcSignalReadResponse([], afterSequence, expiresAt);
        }

        lock (session.Gate)
        {
            session.Signals.RemoveAll(signal => signal.ExpiresAt <= DateTimeOffset.UtcNow);
            var signals = session.Signals
                .Where(signal => signal.Sequence > afterSequence && !string.Equals(signal.SenderRole, recipientRole, StringComparison.Ordinal))
                .Select(signal => new TelehealthLocalWebRtcSignalResponse(signal.Sequence, signal.Kind, signal.Payload))
                .ToArray();
            return new TelehealthLocalWebRtcSignalReadResponse(signals, session.NextSequence, expiresAt);
        }
    }

    private void RemoveExpiredSessions()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var session in _sessions)
        {
            if (session.Value.ExpiresAt <= now)
            {
                _sessions.TryRemove(session.Key, out _);
            }
        }
    }

    private sealed class SessionSignals
    {
        public object Gate { get; } = new();
        public int NextSequence { get; set; }
        public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.MinValue;
        public List<Signal> Signals { get; } = [];
    }

    private sealed record Signal(int Sequence, string SenderRole, string Kind, string Payload, DateTimeOffset ExpiresAt);
}
