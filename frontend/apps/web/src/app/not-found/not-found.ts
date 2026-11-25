import { Component } from '@angular/core'
import { RouterLink } from '@angular/router'
import { Button, PageSeoDirective } from '@pinveria/shared/ui'
import { NgIcon, provideIcons } from '@ng-icons/core'
import { matArrowBackRound, matExploreOffRound } from '@ng-icons/material-symbols/round'
import { HlmEmptyImports } from '@spartan-ng/helm/empty'

@Component({
	selector: 'app-not-found',
	imports: [RouterLink, Button, PageSeoDirective, NgIcon, HlmEmptyImports],
	templateUrl: './not-found.html',
	providers: [provideIcons({ matArrowBackRound, matExploreOffRound })],
	host: { class: 'mx-auto flex w-full max-w-8xl flex-1 flex-col justify-center px-4 py-12 sm:px-6' },
})
export class NotFound {}
