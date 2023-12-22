# Extreal.Integration.Messaging.Redis

## How to test

- Enter the following command in the `WebScripts~` directory.

   ```bash
   yarn
   yarn dev
   ```

- Import the sample MVS from Package Manager.
- Enter the following command in the `MVS/WebScripts` directory.

   ```bash
   yarn
   yarn dev
   ```

   The JavaScript code will be built and output to `/Assets/WebTemplates/Dev`.
- Open `Build Settings` and change the platform to `WebGL`.
- Select `Dev` from `Player Settings > Resolution and Presentation > WebGL Template`.
- Add all scenes in MVS to `Scenes In Build`.
- Start a Messaging Server by running the command below in `RedisServer~` directory.
  - `docker compose up -d`
- Play
  - Native
    - Open multiple Unity editors using ParrelSync.
    - Run
      - Scene: MVS/App/App
  - WebGL
    - See [README](https://github.com/extreal-dev/Extreal.Dev/blob/main/WebGLBuild/README.md) to run WebGL application in local environment.

## Test cases for manual testing

### Host

- Group selection screen
  - Ability to create a group by specifying a name (host)
- VirtualSpace
  - Client can join a group (client join)
  - Clients can leave the group (client exit)
  - Ability to send text (text chat)
  - Ability to return to the group selection screen (host stop)

### Client

- Group selection screen
  - Ability to join a group (join host)
- Virtual space
  - Ability to send text (text chat)
  - Ability to return to the group selection screen (leave host)

### Failure Test
Client
- Join as a third user (joining approval rejected )

Host
- join and then shut down the messaging server (unexpected disconnection)
- Create a group and join after shutting down the messaging server (connection exception)