// Checks a treasure code against the site's public key, entirely in the browser.
// The format is documented in the README ("Verifying a code without this site").

const PREFIX = 'P1R4T3-C0D3{'
const DOMAIN = new TextEncoder().encode('piratepatch/flag/v1\0')
const SIGNATURE_LENGTH = 64
const HEADER_LENGTH = 9

export interface Seal {
  handle: string
  telegramId: bigint
}

export type SealResult =
  | { status: 'genuine'; seal: Seal }
  | { status: 'forged' }
  | { status: 'malformed' }
  | { status: 'unsupported' }

const fromBase64 = (value: string) => Uint8Array.from(atob(value), (c) => c.charCodeAt(0))
const fromBase64Url = (value: string) => fromBase64(value.replaceAll('-', '+').replaceAll('_', '/'))

export async function checkSeal(code: string, publicKeyBase64: string): Promise<SealResult> {
  code = code.trim()
  if (!code.startsWith(PREFIX) || !code.endsWith('}')) return { status: 'malformed' }

  let raw: Uint8Array
  try {
    raw = fromBase64Url(code.slice(PREFIX.length, -1))
  } catch {
    return { status: 'malformed' }
  }
  if (raw.length < HEADER_LENGTH + 1 + SIGNATURE_LENGTH || raw[0] !== 1) return { status: 'malformed' }

  const payload = raw.slice(0, -SIGNATURE_LENGTH)
  const signature = raw.slice(-SIGNATURE_LENGTH)

  let genuine: boolean
  try {
    const key = await crypto.subtle.importKey('raw', fromBase64(publicKeyBase64), 'Ed25519', false, ['verify'])
    genuine = await crypto.subtle.verify('Ed25519', key, signature, new Uint8Array([...DOMAIN, ...payload]))
  } catch {
    return { status: 'unsupported' }
  }
  if (!genuine) return { status: 'forged' }

  const handle = new TextDecoder().decode(payload.slice(HEADER_LENGTH))
  if (!/^[A-Za-z0-9_]{1,32}$/.test(handle)) return { status: 'malformed' }

  const telegramId = new DataView(payload.buffer, payload.byteOffset + 1, 8).getBigInt64(0)
  return { status: 'genuine', seal: { handle, telegramId } }
}
