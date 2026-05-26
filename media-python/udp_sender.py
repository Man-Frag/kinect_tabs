import json
import socket


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

    def close(self):
        self._socket.close()