import type { ActivatedRouteSnapshot, Routes } from '@angular/router'
import { MapsQueryOptions, MapsService, provideMapsBaseUrl } from '@pinveria/maps/data-access'

export const MAPS_ROUTES: Routes = [
	{
		path: '',
		providers: [MapsQueryOptions, MapsService, provideMapsBaseUrl('/api')],
		resolve: { id: (route: ActivatedRouteSnapshot) => route.parent?.parent?.paramMap.get('id') },
		loadComponent: () => import('@pinveria/maps/feature-list').then(module => module.FeatureList),
	},
]
