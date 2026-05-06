FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia il file di progetto specificando la sottocartella
COPY ["BetFlag.BackEnd.Consumer/BetFlag.BackEnd.Consumer.csproj", "BetFlag.BackEnd.Consumer/"]

# Esegui il restore puntando al file nella sottocartella
RUN dotnet restore "BetFlag.BackEnd.Consumer/BetFlag.BackEnd.Consumer.csproj"

# Copia tutto il resto dei file
COPY . .

# Spostati nella cartella del progetto per compilare
WORKDIR "/src/BetFlag.BackEnd.Consumer"
RUN dotnet publish "BetFlag.BackEnd.Consumer.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BetFlag.BackEnd.Consumer.dll"]