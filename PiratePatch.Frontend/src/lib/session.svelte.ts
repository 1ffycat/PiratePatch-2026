import { api, type Me, type SiteConfig } from './api'

/** What the site knows about the visitor, shared by every page. */
class Session {
  config = $state.raw<SiteConfig | null>(null)
  me = $state.raw<Me>({ player: null, solve: null })
  ready = $state(false)
  failed = $state(false)

  async load() {
    try {
      const [config, me] = await Promise.all([api.config(), api.me()])
      this.config = config
      this.me = me
    } catch {
      this.failed = true
    } finally {
      this.ready = true
    }
  }

  async logout() {
    await api.logout()
    this.me = { player: null, solve: null }
  }
}

export const session = new Session()
