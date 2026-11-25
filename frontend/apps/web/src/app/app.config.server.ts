import { mergeApplicationConfig, ApplicationConfig } from '@angular/core'
import { provideServerRendering, withRoutes } from '@angular/ssr'
import { GAMES_SERVER_URL, provideGamesBaseUrl } from '@pinveria/games/data-access'
import { provideMapsBaseUrl } from '@pinveria/maps/data-access'
import { providePageSeo } from '@pinveria/shared/ui'
import { buildHomeOgImagePath } from '@pinveria/shared/util'
import { appConfig } from './app.config'
import { SERVER_ROUTES } from './app.routes.server'

const apiBaseUrl = process.env['PINVERIA_API_URL'] ?? GAMES_SERVER_URL

const serverConfig: ApplicationConfig = {
	providers: [
		provideServerRendering(withRoutes(SERVER_ROUTES)),
		provideGamesBaseUrl(apiBaseUrl),
		provideMapsBaseUrl(apiBaseUrl),
		providePageSeo({
			siteName: 'Pinveria',
			defaultDescription: 'Explore detailed game worlds with collectibles, quests, bosses, and hidden locations.',
			defaultImage: buildHomeOgImagePath(),
			origin: process.env['PUBLIC_SITE_URL'] ?? null,
		}),
	],
}

export const config = mergeApplicationConfig(appConfig, serverConfig)
