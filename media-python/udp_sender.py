import json
import socket
import time as _time


UDP_HOST = "127.0.0.1"
UDP_PORT = 5052


class UdpSender:
    def __init__(self, host=UDP_HOST, port=UDP_PORT):
        self.host = host
        self.port = port
        self._socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    def send_packet(self, packet):
        self._socket.sendto(
            json.dumps(packet).encode("utf-8"),
            (self.host, self.port),
        )

    def send_key_event(self, key, event="keydown", timestamp_ms=None):
        """Send a keystroke event as a UDP packet.

        The packet has the shape:
            {"type": "key", "timestamp_ms": <int>, "event": "keydown", "key": "<name>"}

        Unity receivers that only parse PosePacket will silently ignore it (null
        frame / players), so pose compatibility is preserved.
        """
        payload = {
            "type": "key",
            "timestamp_ms": timestamp_ms if timestamp_ms is not None else int(_time.time() * 1000),
            "event": event,
            "key": key,
        }
        self._socket.sendto(
            json.dumps(payload).encode("utf-8"),
            (self.host, self.port),
        )

    def close(self):
        self._socket.close()