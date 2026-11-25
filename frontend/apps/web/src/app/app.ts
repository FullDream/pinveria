import { Component, inject } from '@angular/core'
import { RouterLink, RouterOutlet } from '@angular/router'
import { BrandLogo, PageLoading, PageProgress, ThemeSwitcher } from '@pinveria/shared/ui'
import { ThemeSettings } from './theme-settings'

@Component({
	selector: 'app-root',
	imports: [RouterLink, RouterOutlet, BrandLogo, ThemeSwitcher, PageProgress],
	templateUrl: './app.html',
	providers: [PageLoading],
	host: { class: 'flex min-h-dvh flex-col bg-background text-foreground' },
})
export class App {
	protected readonly themeSettings = inject(ThemeSettings)
}
