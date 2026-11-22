import { inject } from '@angular/core'
import { Routes } from '@angular/router'
import { PublicGamesQueryOptions } from '@game-atlas/games/data-access'
import { QueryClient } from '@tanstack/angular-query'
import { publicMapGameResolver } from './public-map-game-resolver'

export const APP_ROUTES: Routes = [
	{
		path: '',
		pathMatch: 'full',
		loadChildren: () => import('@game-atlas/games/feature-catalog').then(module => module.GAMES_CATALOG_ROUTES),
	},
	{
		path: 'games',
		children: [
			{
				path: '',
				pathMatch: 'full',
				redirectTo: '/',
			},
			{
				path: ':slug',
				children: [
					{
						path: '',
						pathMatch: 'full',
						title: route => route.paramMap.get('slug') ?? 'Game',
						loadComponent: () =>
							import('@game-atlas/games/feature-detail').then(module => module.GamesFeatureDetail),
					},
					{
						path: 'maps',
						children: [
							{
								path: '',
								pathMatch: 'full',
								redirectTo: '..',
							},
							{
								path: ':mapSlug',
								title: async route => {
									const options = inject(PublicGamesQueryOptions)
									const queryClient = inject(QueryClient)
									const gameSlug = route.paramMap.get('slug')

									if (!gameSlug) return ''

									try {
										const data = await queryClient.fetchQuery(options.getDetail(gameSlug))
										const mapName = data.maps?.find(
											map => map.slug === route.paramMap.get('mapSlug'),
										)?.name

										return mapName ? `${mapName} - ${data.name}` : data.name
									} catch {
										return 'Map unavailable'
									}
								},
								resolve: { game: publicMapGameResolver },
								loadComponent: () =>
									import('@game-atlas/maps/feature-public-map').then(
										module => module.FeaturePublicMap,
									),
							},
						],
					},
				],
			},
		],
	},
	{
		path: '**',
		title: 'Page not found',
		loadComponent: () => import('./not-found/not-found').then(module => module.NotFound),
	},
]
