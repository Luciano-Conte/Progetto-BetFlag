FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["BetFlag.BackEnd.Consumer.csproj", "./"]
RUN dotnet restore "./BetFlag.BackEnd.Consumer.csproj"
COPY . .
RUN dotnet publish "BetFlag.BackEnd.Consumer.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BetFlag.BackEnd.Consumer.dll"]