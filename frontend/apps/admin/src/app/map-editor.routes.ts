import type { FeatureEditor } from '@pinveria/maps/feature-editor'
import { inject } from '@angular/core'
import type { ActivatedRouteSnapshot, Routes } from '@angular/router'
import { GamesService } from '@pinveria/games/data-access'
import { MapsService, provideMapHubUrl, provideMapsBaseUrl } from '@pinveria/maps/data-access'
import { catchError, map, of } from 'rxjs'

export const MAP_EDITOR_ROUTES: Routes = [
	{
		path: '',
		canDeactivate: [(component: FeatureEditor) => component.canLeave()],
		providers: [MapsService, provideMapsBaseUrl('/api'), provideMapHubUrl('/api/hubs/map')],
		resolve: {
			gameId: (route: ActivatedRouteSnapshot) => route.parent?.paramMap.get('gameId'),
			mapId: (route: ActivatedRouteSnapshot) => route.parent?.paramMap.get('mapId'),
			gameName: (route: ActivatedRouteSnapshot) =>
				inject(GamesService)
					.getGame(route.parent?.paramMap.get('gameId') ?? '')
					.pipe(
						map(game => game.name),
						catchError(() => of('Game')),
					),
		},
		loadComponent: () => import('@pinveria/maps/feature-editor').then(module => module.FeatureEditor),
	},
]
