# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:11.0-preview AS build
WORKDIR /src

COPY dotnet-tools.json ./
COPY src/Pinveria.Domain/Pinveria.Domain.csproj src/Pinveria.Domain/
COPY src/Pinveria.Application/Pinveria.Application.csproj src/Pinveria.Application/
COPY src/Pinveria.Infrastructure/Pinveria.Infrastructure.csproj src/Pinveria.Infrastructure/
COPY src/Pinveria.Api/Pinveria.Api.csproj src/Pinveria.Api/

RUN dotnet restore src/Pinveria.Api/Pinveria.Api.csproj
RUN dotnet tool restore

COPY src/ src/

RUN dotnet publish src/Pinveria.Api/Pinveria.Api.csproj \
	-c Release \
	-o /app/publish \
	/p:UseAppHost=false

RUN ConnectionStrings__Pinveria="Host=localhost;Database=pinveria;Username=pinveria;Password=build-placeholder" \
	dotnet ef migrations bundle \
	--project src/Pinveria.Infrastructure \
	--startup-project src/Pinveria.Api \
	--configuration Release \
	--self-contained \
	-r linux-x64 \
	-o /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview AS runtime
WORKDIR /app

RUN apt-get update \
	&& apt-get install -y --no-install-recommends curl libglib2.0-0 libexpat1 \
	&& rm -rf /var/lib/apt/lists/* \
	&& install -d -m 0750 -o app -g app /var/lib/pinveria/maps /var/lib/pinveria/media /var/lib/pinveria/dpkeys

COPY --from=build /app/publish ./
COPY --from=build --chmod=0755 /app/efbundle /app/efbundle

ENV ASPNETCORE_URLS=http://+:8080 \
	ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
USER app

ENTRYPOINT ["dotnet", "Pinveria.Api.dll"]
