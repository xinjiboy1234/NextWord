FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Backend/NextWord.Domain/NextWord.Domain.csproj Backend/NextWord.Domain/
COPY Backend/NextWord.Infrastructure/NextWord.Infrastructure.csproj Backend/NextWord.Infrastructure/
COPY Backend/NextWord.Api.Endpoints/NextWord.Api.Endpoints.csproj Backend/NextWord.Api.Endpoints/
COPY Backend/NextWord.Api/NextWord.Api.csproj Backend/NextWord.Api/
RUN dotnet restore Backend/NextWord.Api/NextWord.Api.csproj
COPY Backend/ ./Backend/
RUN dotnet publish Backend/NextWord.Api/NextWord.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "NextWord.Api.dll"]
