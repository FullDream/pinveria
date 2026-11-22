import { type Routes } from '@angular/router'

export const GAMES_CATALOG_ROUTES: Routes = [
	{
		path: '',
		title: 'Interactive Game Maps',
		loadComponent: () => import('./feature-catalog').then(module => module.FeatureCatalog),
	},
]
