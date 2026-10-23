import base64, sys
from cryptography.hazmat.primitives.serialization import load_pem_public_key

code = sys.argv[1]  # P1R4T3-C0D3{...}
raw = base64.urlsafe_b64decode(code[len("P1R4T3-C0D3{"):-1] + "==")
payload, signature = raw[:-64], raw[-64:]

load_pem_public_key(open("signing-key.pem", "rb").read()).verify(signature, b"piratepatch/flag/v1\0" + payload)  # raises if forged
print("genuine:", int.from_bytes(payload[1:9], "big", signed=True), payload[9:].decode())
