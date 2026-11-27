import { IMAGE_CONFIG, IMAGE_LOADER, ImageLoaderConfig } from '@angular/common'
import { EnvironmentProviders, InjectionToken, makeEnvironmentProviders } from '@angular/core'
import {
	buildOptimizedMediaUrl,
	isMediaImageFormat,
	MEDIA_IMAGE_BREAKPOINTS,
	MediaImageFormat,
} from '@pinveria/shared/util'

export const MEDIA_IMAGE_OPTIMIZATION_ENABLED = new InjectionToken<boolean>('MEDIA_IMAGE_OPTIMIZATION_ENABLED', {
	providedIn: 'root',
	factory: () => false,
})

const resolveLoaderFormat = (config: ImageLoaderConfig): MediaImageFormat => {
	const rawFormat = config.loaderParams?.['format']

	return isMediaImageFormat(rawFormat) ? rawFormat : 'auto'
}

export const mediaImageLoader = (config: ImageLoaderConfig): string =>
	buildOptimizedMediaUrl(config.src, {
		width: config.width,
		format: resolveLoaderFormat(config),
	})

export const provideMediaImageOptimization = (): EnvironmentProviders =>
	makeEnvironmentProviders([
		{
			provide: MEDIA_IMAGE_OPTIMIZATION_ENABLED,
			useValue: true,
		},
		{
			provide: IMAGE_CONFIG,
			useValue: {
				breakpoints: [...MEDIA_IMAGE_BREAKPOINTS],
			},
		},
		{
			provide: IMAGE_LOADER,
			useValue: mediaImageLoader,
		},
	])
