# Extreal.Integration.Messaging.Redis

## How to test

- Enter the following command in the `WebScripts~` directory.

   ```bash
   yarn
   yarn dev
   ```

- Import the sample MVS from Package Manager.
- Enter the following command in the `MVS/WebScripts~` directory.

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

### Functional Test

- Group selection screen
  - Ability to create a group by specifying a name with host role selected (create group)
  - Ability to list groups with client role selected (list groups)
  - Ability to join a group (join group)
  - Ability to return to title screen (dispose messaging client)
- VirtualSpace
  - Clients can join the group (client joins)
  - Clients can leave the group (client leaves)
  - Ability to send text (send text chat)
  - Ability to see received text (receive text chat)
  - Ability to return to group selection screen with host role selected (delete group)
  - Ability to return to group selection screen with client role selected (leave group)

### Failure Test

- Join as a third user (joining approval rejected )
- join and then shut down the messaging server (unexpected leave)
- Create a group and join after shutting down the messaging server (connection exception)
