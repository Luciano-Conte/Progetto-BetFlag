# 🚀 BetFlag Microservices Architecture

Questo progetto è una simulazione di un sistema di scommesse distribuito (Backend) costruito con un'architettura a microservizi. Dimostra come gestire operazioni sincrone (API) e asincrone (Background Worker) utilizzando **.NET 10**, **RabbitMQ**, **Redis**, **Docker** e **SignalR**.

## 🏗️ Architettura del Sistema

Il sistema è composto da diversi container Docker che comunicano tra loro:

1.  **Bet API (.NET 10):** Espone gli endpoint REST per ricevere le scommesse. Aggiorna immediatamente il saldo dell'utente nel database e invia un messaggio alla coda per l'elaborazione asincrona. **Include un Hub SignalR per inviare notifiche push ai client.**
2.  **Bet Consumer (Background Worker .NET 10):** Resta in ascolto sulla coda RabbitMQ. Appena arriva un messaggio, lo preleva e simula la validazione/registrazione della scommessa, notificando l'API al termine del processo.
3.  **RabbitMQ:** Il Message Broker che gestisce la coda (`bet_queue`) disaccoppiando l'API dal Consumer.
4.  **SQL Server 2022:** Il database relazionale che memorizza gli utenti, i loro saldi e l'intero storico delle scommesse (con i relativi stati Pending/Processed).
5.  **Redis:** Cache in memoria dove vengono memorizzate e gestite le quote (odds).

## 🛠️ Tecnologie Utilizzate

* C# / .NET 10 (ASP.NET Core Web API & Worker Service)
* Entity Framework Core (SQL Server)
* RabbitMQ (Message Broker)
* Docker & Docker Compose
* Redis OSS
* SignalR (Real-time WebSockets)

## ⚙️ Prerequisiti

Per eseguire questo progetto sul tuo computer locale, devi avere installato:
* [Docker Desktop](https://docs.docker.com/desktop/setup/install/windows-install/)

## 🚀 Come avviare il progetto

1.  Posizionarsi nella cartella principale del progetto (dove è presente il file `docker-compose.yml`).
2.  Eseguire il comando:
    ```bash
    docker-compose up --build
    ```

*L'avvio potrebbe richiedere qualche istante la prima volta per scaricare le immagini di SQL Server, RabbitMQ e Redis OSS.*

## 🎮 Come testare il flusso

Una volta che i container sono in esecuzione, puoi testare il sistema:

**1. Apri il Terminale delle Notifiche:**
   * Vai all'indirizzo: `http://localhost:8080/index.html` (vedrai il messaggio "In attesa di connessione...").

**2. Invia una scommessa tramite Swagger:**
   * Apri il browser su: `http://localhost:8080/swagger`
   * Effettua una richiesta **POST** (`/api/bet/place`) con questo payload JSON:
     ```json
     {
       "userId": 1,
       "eventId": 1,
       "sign": "2",
       "amount": 10,
       "odds": 2.50
     }
     ```

**3. Verifica il Risultato:**
   * **Nel Terminale Notifiche:** Vedrai apparire istantaneamente il messaggio ✅ "Scommessa processata" non appena il Consumer finisce il lavoro.
   * **Nei Log Docker:** Vedrai l'API che scala il saldo (`UPDATE [Users] SET [Balance]...`) e il Consumer che preleva il messaggio.

**4. Visualizza lo Storico Scommesse:**
   * Sempre su Swagger, usa l'endpoint **GET** (`/api/bet/history`) per recuperare lo storico, inserendo `1` come `userId`.
   * Vedrai un array JSON con tutte le giocate dell'utente e il loro stato aggiornato (Processed).

**5. Spegnimento:**
   * Per fermare i container: `docker-compose down`
   * Per fermare i container e pulire anche i volumi (reset database): `docker-compose down -v`

## 📊 Dashboard e Interfacce

* **Real-Time Terminal:** `http://localhost:8080/index.html` (Interfaccia SignalR)
* **Swagger UI:** `http://localhost:8080/swagger`
* **RabbitMQ Management UI:** `http://localhost:15672` (Credenziali: `guest` / `guest`)
* **SQL Server:** Esposto sulla porta `1433` (Credenziali SA configurate nel `docker-compose.yml`).

---
*Progetto a scopo didattico.*