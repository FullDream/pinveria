import { mergeApplicationConfig, ApplicationConfig } from '@angular/core'
import { provideServerRendering, withRoutes } from '@angular/ssr'
import { GAMES_SERVER_URL, provideGamesBaseUrl } from '@game-atlas/games/data-access'
import { provideMapsBaseUrl } from '@game-atlas/maps/data-access'
import { providePageSeo } from '@game-atlas/shared/ui'
import { buildHomeOgImagePath } from '@game-atlas/shared/util'
import { appConfig } from './app.config'
import { SERVER_ROUTES } from './app.routes.server'

const apiBaseUrl = process.env['GAME_ATLAS_API_URL'] ?? GAMES_SERVER_URL

const serverConfig: ApplicationConfig = {
	providers: [
		provideServerRendering(withRoutes(SERVER_ROUTES)),
		provideGamesBaseUrl(apiBaseUrl),
		provideMapsBaseUrl(apiBaseUrl),
		providePageSeo({
			siteName: 'GameAtlas',
			defaultDescription: 'Explore detailed game worlds with collectibles, quests, bosses, and hidden locations.',
			defaultImage: buildHomeOgImagePath(),
			origin: process.env['PUBLIC_SITE_URL'] ?? null,
		}),
	],
}

export const config = mergeApplicationConfig(appConfig, serverConfig)
