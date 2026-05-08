# 🚀 BetFlag Microservices Architecture

Questo progetto è una simulazione di un sistema di scommesse distribuito (Backend) costruito con un'architettura a microservizi. Dimostra come gestire operazioni sincrone (API) e asincrone (Background Worker) utilizzando **.NET 10**, **SQL Server**, **RabbitMQ**, **Redis**, **Docker** e **SignalR**.

## 🏗️ Architettura del Sistema

Il sistema è composto da diversi container Docker che comunicano tra loro:

1.  **Bet API (.NET 10):** Espone gli endpoint REST protetti. Riceve la richiesta di scommessa, la salva in stato *Pending* e invia una richiesta di pagamento alla coda `wallet_requests`. Contiene anche un Hub SignalR per inviare notifiche push in tempo reale all'utente e sincronizza il saldo locale a operazione conclusa.
2.  **Bet Wallet (Worker .NET 10):** Ascolta la coda `wallet_requests`, verifica se l'utente ha saldo sufficiente nel proprio database dedicato, scala i fondi e pubblica l'esito (Successo/Fallimento) sulla coda delle risposte `bet_responses`.
3.  **Bet Consumer (Worker .NET 10):** Resta in ascolto sulla coda `bet_responses`. Preleva l'esito dal Wallet e chiama l'endpoint di conferma dell'API per far passare la scommessa in stato *Processed* o *Rejected*.
4.  **RabbitMQ:** Il Message Broker che gestisce le code (`wallet_requests` e `bet_responses`), garantendo la resilienza e il disaccoppiamento tra API, Wallet e Consumer.
5.  **SQL Server 2022:** Ospita due database separati: `BetFlagDb` (per lo storico scommesse e copia locale utenti dell'API) e `BetFlagWalletDb` (dove sono presenti i saldi reali degli utenti).
6.  **Redis:** Cache in memoria dove vengono memorizzate e gestite le quote (odds).

## 🛠️ Tecnologie Utilizzate

* C# / .NET 10 (ASP.NET Core Web API & Worker Service)
* Entity Framework Core (SQL Server)
* Autenticazione JWT (JSON Web Token)
* RabbitMQ (Message Broker)
* Docker & Docker Compose
* Redis OSS
* SignalR (Real-Time WebSockets)

## 🛡️ Resilienza e Pattern Avanzati

Per garantire la massima affidabilità in un contesto finanziario (Betting), il sistema implementa pattern avanzati per la gestione dei fallimenti di rete:

**Idempotenza Distribuita:** In un sistema a microservizi, un messaggio può essere consegnato più di una volta a causa di retry di rete.
  * **Problema:** Senza protezione, un utente potrebbe vedersi addebitare la stessa scommessa due volte se il messaggio RabbitMQ viene duplicato.
  * **Soluzione:** Sia il **Bet Wallet** che la **Bet API** implementano il controllo dell'idempotenza tramite la tabella `ProcessedTransactions`. Ogni operazione viene registrata con il suo `BetId` univoco; se un messaggio duplicato arriva, il sistema lo riconosce, non ripete l'addebito e restituisce l'esito positivo della transazione già avvenuta.

## ⚙️ Prerequisiti

Per eseguire questo progetto sul tuo computer locale, devi avere installato:
* [Docker Desktop](https://docs.docker.com/desktop/setup/install/windows-install/)

## 🚀 Come Avviare il Progetto

1.  Avviare Docker Desktop.
2.  Tramite un terminale (Windows PowerShell o CMD), posizionarsi nella cartella principale del progetto (dove è presente il file `docker-compose.yml`).
3.  Eseguire il comando:
    ```bash
    docker-compose up --build
    ```

*L'avvio potrebbe richiedere qualche istante la prima volta per scaricare le immagini di SQL Server, RabbitMQ e Redis OSS.*

## 🎮 Come Testare il Flusso

Una volta che i container sono in esecuzione, segui questi passaggi per testare il sistema:

**1. Genera il Token JWT:**
   * Apri il browser su Swagger: `http://localhost:8080/swagger`
   * Vai sull'endpoint **POST** `/api/auth/login` e inserisci credenziali valide (es. Lucia / Pass123) per ottenere il tuo Token. Copialo (senza le virgolette).

**2. Apri il Terminale delle Notifiche (Frontend):**
   * Vai all'indirizzo: `http://localhost:8080/index.html`
   * Incolla il Token nella casella di testo e clicca "Connetti". Vedrai il messaggio di avvenuta connessione e di benvenuto, con il saldo dell'utente.

**3. Invia una Scommessa Tramite Swagger:**
   * Torna su Swagger, clicca in alto su **Authorize** e incolla il token nel campo Value.
   * Effettua una richiesta **POST** (`/api/bet/place`) con questo payload JSON (l'ID utente verrà estratto automaticamente e in modo sicuro dal token):
     ```json
     {
       "eventId": 1,
       "sign": "2",
       "amount": 10,
       "odds": 2.50
     }
     ```
   * *Se si prova a inserire un importo superiore al saldo dell'utente (es. 1000€) la scommessa viene rifiutata (Rejected).*

**4. Verifica il Risultato:**
   * **Nel Terminale Notifiche:** Vedrai apparire istantaneamente "✅ Scommessa confermata! ID: 1. Il tuo nuovo saldo è di: 90.00€" oppure "❌ Scommessa rifiutata: Saldo insufficiente".
   * **Nei Log Docker:** Vedrai il Wallet elaborare la transazione e il Consumer notificare l'API.

**5. Visualizza lo Storico Scommesse:**
   * Sempre su Swagger, usa l'endpoint **GET** (`/api/bet/history`).
   * Vedrai un array JSON con tutte le giocate dell'utente e il loro stato aggiornato (*Pending*, *Processed*, *Rejected*).

**6. Spegnimento:**
   * Per fermare i container: `docker-compose down`
   * Per fermare i container e pulire anche i volumi (reset database): `docker-compose down -v`

## 🧪 Test di Resilienza (Stress Test Idempotenza)

Per verificare che il sistema gestisca correttamente i messaggi duplicati (evitando doppi addebiti), puoi simulare un errore di rete forzando un messaggio manuale su RabbitMQ:

1. **Esegui una scommessa normale** tramite Swagger.
2. **Accedi a RabbitMQ Management**: `http://localhost:15672` (user: `guest` / pass: `guest`).
3. Vai nella sezione **Queues and Streams** e seleziona la coda `wallet_requests`.
4. Scorri fino a **Publish message** e inserisci nel campo Payload:
   ```json
   { "BetId": 1, "Amount": 10.0 }
   ```
5. **Osserva i Log:**
   * Nel log di **bet-wallet**, vedrai: `🛡️ TRANSAZIONE IDEMPOTENTE: BetId 1 già processato. Ignoro l'addebito...`
   * Nel log di **bet-api**, vedrai: `🛡️ Messaggio duplicato ignorato per BetId: 1.`
   * Il saldo dell'utente rimarrà invariato, confermando che la protezione ha funzionato.

## 🔍 Monitoraggio e Log (Terminale)

Per controllare cosa succede nei diversi container Docker e vedere la comunicazione tra i microservizi, puoi aprire un nuovo terminale ed eseguire questi comandi (aggiungi `-f` per seguirli in tempo reale):

* **Log dell'API:** `docker-compose logs -f bet-api`
* **Log del Wallet:** `docker-compose logs -f bet-wallet` (Utile per vedere i pagamenti o i rifiuti per saldo insufficiente).
* **Log del Consumer:** `docker-compose logs -f bet-consumer` (Vedi i messaggi ricevuti da RabbitMQ e l'invio all'API).

## 📊 Dashboard e Interfacce

* **Real-Time Terminal:** `http://localhost:8080/index.html` (Interfaccia SignalR)
* **Swagger UI:** `http://localhost:8080/swagger`
* **RabbitMQ Management UI:** `http://localhost:15672` (Credenziali: `guest` / `guest`)
* **SQL Server:** Esposto sulla porta `1433` (Credenziali SA configurate nel `docker-compose.yml`).

---
*Progetto a scopo didattico.*