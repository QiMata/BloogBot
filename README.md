# Westworld of Warcraft (WWoW)

**Westworld of Warcraft (WWoW)** is a simulation platform that transforms a World of Warcraft–like server into a living world populated by both real players and AI-driven bots. Inspired by HBO’s *Westworld*, this project aims to create AI-controlled characters **indistinguishable from human players** in behavior. In WWoW, AI agents roam the game world, quest, fight monsters, interact with the environment (and eventually with players) just as a human would – providing a rich testbed for **agent-based AI** in a complex game environment.

## Documentation Map

The repository is organized into several large subsystems. Each has its own README with deep dives, quick-start instructions, and API notes:

| Area | Description |
| --- | --- |
| [Services](./Services/README.md) | Distributed worker services that coordinate bots, navigation, prompts, and decision making. |
| [Exports](./Exports/README.md) | Core libraries and native components injected into the WoW client or consumed by services. |
| [UI](./UI/README.md) | User interfaces for orchestrating bots and monitoring systems. |
| [WWoW.AI](./WWoW.AI/README.md) | Experimental AI agents and supporting tooling. |
| [Documentation](./docs/README.md) | Technical documentation including physics system details. |
| [Recorded Tests Shared Library](./WWoW.RecordedTests.Shared/README.md) | Orchestration primitives for automated, recorded integration tests. |
| [Recorded Test Ideas](./WWoW.RecordedTests.Shared/RECORDED_TEST_IDEAS.md) | Backlog of high-value encounters to automate and film. |

Use this README for a high-level overview of the project’s goals, capabilities, and roadmap. Follow the links above for subsystem-specific documentation and build instructions.

## Purpose and Vision

WWoW’s primary goal is to explore advanced **agentic AI behavior** in an open-world MMORPG setting. By blurring the lines between human and bot players, we can study and push the boundaries of:

* **Human-Bot Indistinguishability:** Can an AI-controlled player mimic human playstyles, decision-making, and even social interaction convincingly? WWoW is a playground to find out.
* **Game Environment Simulation:** Populate a game server with autonomous agents to simulate a lively game world even with few or no human participants. This can be used for game testing, AI research, or simply creating a unique gameplay experience.
* **Agent-Based AI Techniques:** The project serves as a research platform for developing and testing AI techniques (state machines, reinforcement learning, planning, etc.) in a real-time environment. The WoW game world provides a complex, dynamic environment for AI agents to navigate and learn.

Ultimately, WWoW is about **intellectual exploration** – understanding how to build AI agents that can live and behave in a virtual world, not about exploiting the game. (Indeed, WWoW **does not support or work on modern official WoW servers**, and is intended for private/research use on legacy versions.)

## Features and Current Functionality

WWoW is built upon an open-source WoW bot framework (originally known as *BloogBot*), and extends it into a full simulation platform. The current features include:

* **Automated Player Characters:** WWoW’s bots can create and control WoW characters to fight monsters, level up, and travel the world **without human input**. Each bot runs in one of two interchangeable modes — *foreground* (injected into a real `WoW.exe` for true client parity) or *background* (headless, emulating the WoW network protocol with no game client) — both driven by the same core behavior engine.
* **Multi-Expansion Support:** The platform currently works with WoW Classic era game clients – Vanilla (1.12.1), Burning Crusade (2.4.3), and Wrath of the Lich King (3.3.5). This corresponds to popular private server versions (e.g. Kronos, TurtleWoW, Atlantiss, Warmane) and any self-hosted server based on MaNGOS/TrinityCore for those expansions. (Modern retail WoW is not supported due to vastly different code and anti-cheat mechanisms.)
* **Questing and Combat AI:** Bots come with class-specific “profiles” that dictate their combat rotations and behaviors. For example, a Frost Mage bot knows how to cast frost spells, kite enemies, and use food/drink to recover mana. All provided class profiles (e.g. FrostMageBot, etc.) are functional and can be customized. The AI uses a state-machine approach – e.g. states for *Idle*, *Patrol*, *Combat*, *Rest*, *Dead* – to manage behavior. Bots will engage hostile mobs, use spells/abilities, loot corpses, and even retreat or rest when needed.
* **Navigation and Pathfinding:** To move convincingly and avoid obstacles, WWoW uses a navigation mesh system. You’ll generate **movement maps (navmeshes)** from the game data, which the bot uses for pathfinding. Bots can intelligently move toward targets, roam between towns and wilderness, and navigate around terrain features. The pathfinding is based on compiled game world data (similar to Recast/Detour algorithms) to ensure the AI knows where it can walk or where obstacles are.
* **Hotspots and Grinding Areas:** WWoW defines **hotspots** – locations in the game world optimal for certain level ranges or objectives. Each hotspot includes waypoints for the bot to patrol, the level range of monsters, and references to nearby NPCs (like innkeepers or vendors). Bots can switch between hotspots as they level up, creating a progression (e.g. starting in a newbie zone, then moving to tougher areas as they gain levels).
* **NPC Interaction (Vendors/Repair):** Bots can interact with non-player characters for basic needs. For example, when gear durability gets low or bags are full, a bot will travel to a repair vendor or shop to sell junk and restock ammunition. Innkeepers are known to the bot as well, potentially for setting hearthstones or buying food/drink. This ensures the AI can sustain itself over long play sessions without human help.
* **Death and Recovery:** If a bot character dies (for example, overwhelmed by enemies), it will automatically handle corpse retrieval and resurrection. It uses the navmesh to navigate from the graveyard back to its corpse. (If the area is too dangerous or the death point is unreachable, the bot may respawn at a spirit healer after a timeout.)
* **Persistence (local-only):** WWoW persists data across **local** stores — there is no cloud-database dependency. The MaNGOS server holds the game world in **MySQL/MariaDB** (read-mostly from the bot's side — mutate only via SOAP), the bot caches derived game knowledge (quests, dialog/storylines) in **SQLite**, and the decision-engine knowledge base plus agent memory live in **PostgreSQL**. See [`docs/data-model.md`](./docs/data-model.md) for the full data model.
* **Recorded Test Harness:** The [`WWoW.RecordedTests.Shared`](./WWoW.RecordedTests.Shared/README.md) library bundles orchestration helpers for filmed regression tests. It ensures private servers are available, coordinates foreground/background runners, and centralizes artifact capture for automated video evidence.
* **Stealth Operation (foreground mode):** A foreground bot runs inside the WoW client process itself, which gives it direct access to game functions and makes it harder to detect by the game's anti-bot measures; WWoW also disables the legacy Warden anti-cheat in older clients upon injection. (Background bots sidestep the client entirely.) The approach is similar to how some cheat bots work, but here it is purposed for creating a believable simulation rather than gaining unfair advantage in competitive play.
* **Extensibility:** The architecture is modular. You can create new **bot profiles** (for classes or even custom behaviors) by implementing the AI logic for that profile (e.g., how a Warrior fights vs. how a Mage fights). You can also extend the system with new types of agents or scripts – for instance, creating a “QuestingBot” that completes quests, or a “ChatBot” that engages in in-game chat using AI. Developers can use the provided API of the bot to get information about the game state (e.g. player health, nearby units, inventory) and take actions (move, attack, cast spells, etc.).

**Technical Highlights:** Under the hood, WWoW is a set of **C#/.NET 8** worker services (orchestrated with **.NET Aspire**) plus a few native **C++** components. In *foreground* mode, `WoWStateManager` launches the WoW client and injects the native `Loader.dll` (`Exports/Loader`), which bootstraps the .NET 8 runtime inside the game process and loads `ForegroundBotRunner`; the injected bot then controls the game through direct memory read/write and game-function calls. In *background* mode, `BackgroundBotRunner` drives a character purely over the WoW network protocol (`Exports/WoWSharpClient`) with no game client at all. The project also makes use of supporting libraries and data:

* **Navigation meshes:** Precomputed movement maps generated from the client's map data by [`tools/MmapGen`](./tools/MmapGen/), consumed by the Detour/Recast pathfinder in `Exports/Navigation`.
* **System.Text.Json:** Configuration (`appsettings.json`) and profile/activity definitions.
* **AI providers:** Decision-making and dialog use a **local LLM via [Ollama](https://ollama.com)** by default, with optional hosted providers (OpenAI / Azure OpenAI).

Finally, note that WWoW remains a **work-in-progress hobby project** – while many features work, there may be quirks or bugs, and not all WoW game content is handled yet. It’s already capable of basic leveling and combat autonomously, but making bots truly **human-like** in all aspects (grouping, chatting, complex quest logic, PvP tactics, etc.) is an ongoing effort.

## Installation and Setup

Follow these steps to build and run WWoW on your own machine. For deeper
references see [`docs/DEVELOPMENT_GUIDE.md`](./docs/DEVELOPMENT_GUIDE.md),
[`docs/local-development.md`](./docs/local-development.md), and
[`docs/BUILD.md`](./docs/BUILD.md).

1. **Prerequisites:**

   * **Windows PC** – WWoW targets Windows and, in foreground mode, injects into the Windows WoW client.
   * **World of Warcraft client (legacy):** a supported build — **1.12.1 (5875)**, **2.4.3 (8606)**, or **3.3.5a (12340)**. WWoW does *not* supply game files; bring your own from a private-server / MaNGOS install.
   * **.NET 8 SDK** – required for everything; pinned to the 8.0.x line by [`global.json`](./global.json). A newer-only SDK (9.x/10.x) is rejected with an explicit message so local builds stay in step with CI.
   * **PowerShell 7+** (`pwsh`) – the scripts in [`scripts/`](./scripts/) are the supported build/test interface.
   * **Visual Studio 2022 or 2025** with the **Desktop development with C++** workload (Platform Toolset **v145**) – only needed to build the native C++ projects (`Exports/Navigation`, `Exports/Loader`, `Exports/FastCall`). Pure .NET work does not need it.
   * **Git LFS** – large physics-recording fixtures are stored via LFS.
   * **Docker Desktop** – runs the local MaNGOS server stack (database + realmd + mangosd + SOAP). The inner build/test loop does not need it.
   * **(Optional) `aspire` workload** – `dotnet workload install aspire`, only to launch the .NET Aspire AppHost.

2. **Clone & restore:**

   ```powershell
   git clone <repo-url> WestworldOfWarcraft
   cd WestworldOfWarcraft
   pwsh ./scripts/bootstrap.ps1     # verifies the .NET 8 SDK, restores packages + tools
   ```

3. **Build:** Build the managed solution plus the native C++ DLLs:

   ```powershell
   pwsh ./scripts/build.ps1 -Native   # WestworldOfWarcraft.sln + Navigation/Loader/FastCall
   ```

   > **Kill `WoW.exe` before building.** Foreground injection copies the native DLLs from the build output; a running `WoW.exe` locks them and causes `MSB3027` copy errors. `scripts/build.ps1` warns when it sees a running client — close it (or `taskkill /F /PID <pid>` for that specific PID) and rebuild.

4. **Start the local server stack (Docker):** Bring up the MaNGOS stack (database, realm/world servers, SOAP) — see [`docs/DOCKER_STACK.md`](./docs/DOCKER_STACK.md) for the full walkthrough and the `.env` values the stacks expect. For the Windows all-in-one container:

   ```powershell
   docker compose -f docker-compose.windows.yml up -d
   ```

5. **Configure:** Bot configuration lives in `Services/WoWStateManager/appsettings.json` (full schema in [`docs/CONFIG_SCHEMA.md`](./docs/CONFIG_SCHEMA.md)):

   * Point **`GameClient:ExecutablePath`** at your `WoW.exe` for foreground mode (e.g. `"D:\\World of Warcraft\\WoW.exe"`).
   * The character roster is a set of `Settings/Configs/*.config.json` entries (`CharacterSettings`): `AccountName`, `CharacterClass` / `CharacterRace`, and **`RunnerType`** = `Foreground` (DLL-injected) or `Background` (headless). Missing accounts are created on the server via SOAP.
   * There is **no** `botSettings.json` or `Bootstrapper` settings file — those belonged to the retired standalone injector.

6. **Run:** Launch the orchestrated stack via the .NET Aspire AppHost:

   ```powershell
   dotnet run --project UI/Systems/Systems.AppHost
   ```

   This starts `WoWStateManager` (bot lifecycle + foreground injection, ports 9000/9001), `PathfindingService` (9002), `SceneDataService` (9003), and the supporting services. `WoWStateManager` then launches and injects foreground bots and/or spins up headless background bots according to your config. The WPF operator console (`UI/WoWStateManagerUI`) can attach for monitoring.

7. **In-game behavior:** Once a bot is in the world it initializes within a few seconds and acts on its profile — targeting suitable enemies, running its combat rotation, looting, resting to eat/drink at low health or mana, and traveling between areas as it levels. A foreground bot runs inside the real client, so you can watch the character move; a background bot has no window.

8. **Troubleshooting:**

   * **Build can't copy native DLLs (`MSB3027`):** a `WoW.exe` is running and locking them — close it (or `taskkill /F /PID <pid>` for that specific PID) and rebuild.
   * **Native build fails / MSBuild not found:** install the Visual Studio **Desktop development with C++** workload and the **v145** toolset; `pwsh ./scripts/build.ps1 -Native` prints exactly what is missing.
   * **"Compatible SDK not found":** install the **.NET 8** SDK — a 9.x/10.x-only machine is rejected on purpose so local builds match CI.
   * **Bot doesn't act / wrong offsets:** confirm the client build is exactly **1.12.1 (5875)**, **2.4.3 (8606)**, or **3.3.5a (12340)** — memory offsets are version-specific.
   * **Vanilla auto-attack:** on 1.12 clients, place the **Auto-Attack** ability in the rightmost main action-bar slot so the bot can initiate melee correctly.
   * **Injection blocked:** anti-virus / firewall may flag process injection; allow the build output (and try running elevated).
   * More: [`docs/troubleshooting.md`](./docs/troubleshooting.md).

Once it's running you effectively have your own small *Westworld* in Azeroth — and you can populate the world with many characters at once by adding entries to the roster (headless background bots in particular run many-to-a-machine cheaply).

## Usage Examples

What can you do with WWoW? Here are a few scenarios to illustrate how the platform might be used:

* **Single AI Adventurer:** Run one bot on a private server. For example, start a Human Mage at level 1 in Northshire Abbey. The WWoW bot will automatically accept the introductory quests (if questing logic is present or via grinding it will level up), kill wolves and kobolds, loot them, and level up. It will use Frost Nova and Fireball according to its FrostMage profile logic. As it gains levels, it may move to new areas (e.g. Goldshire) following the configured hotspots. You can observe this character progressing through the world hands-free. To a nearby human observer, the character looks just like any other player going about their leveling journey.
* **Party of Bots (PvE Exploration):** You could configure multiple bots to run together, simulating a full party. For instance, run a Warrior bot as a tank, a Priest bot as a healer, and several DPS bots. With additional scripting, they could even coordinate (e.g., assisting the warrior’s target). They could collectively take on harder content like dungeons or elite monsters. This is excellent for testing how AI agents can cooperate and fulfill MMORPG group roles.
* **Mixed Reality Server:** Host a small WoW server where a few human friends play alongside bot players. The bots might fill the world to make it lively: some could be grinding in the fields, others could be wandering in town. As a human player, you can trade with them, group up, or duel them. Ideally, the bots behave naturally enough that your friends might not immediately realize that “Nightelf Hunter Jane” over there is AI-controlled. This scenario brings the *Westworld* concept to life – a blend of real and AI characters sharing a virtual world.
* **AI Behavior Research:** Use WWoW as a research environment. For example, you could log all bot actions and state transitions to the database for offline analysis (thanks to the data logging feature). Researchers could analyze this data to find patterns or train machine learning models. One might replace the built-in decision-making with a custom AI (e.g., a reinforcement learning agent that learns to optimize XP gain, or a language model that generates in-game chat messages to make the bot seem more social). WWoW provides the scaffolding to plug in such experimental AI modules within a real game world.
* **Headless Simulation:** Background bots already run **headless** — `BackgroundBotRunner` drives a character purely over the WoW network protocol with no game client, so you can run many bots on one machine for load testing or large-scale simulations (e.g. populate a test realm with dozens of AI players and watch how the server and economy react). Foreground bots, which run inside a real client for full rendering/physics parity, are heavier and used when client fidelity matters.

**Interacting with Bots:** At present, bot characters will respond to the game world (combat, NPCs) but have limited social interaction with players. They won’t initiate chat on their own (unless you extend their code to do so). However, one could extend WWoW to give bots conversational abilities using AI (for example, integrate an NLP model so they can respond to whispers or messages). Part of the vision is to eventually have bots that *talk* and *group* like players. For now, you can steer bots through the WPF operator console or with in-game GM/chat commands (e.g., telling a bot to pause, or go to a certain location). As a player, you can also try to `'/follow'` a bot or invite it to a party (if programmed, bots could accept invites). These interactions are areas for future improvement.

In summary, WWoW usage can range from a passive observer (watching your AI toon do its thing), to an active participant in a mixed world, to a developer tweaking AI algorithms. It’s a sandbox – feel free to experiment!

## Development and Contribution Guidelines

Contributions to WWoW (Westworld of Warcraft) are welcome! This project is at the intersection of game development, AI, and systems programming – there are many ways to improve it. If you’d like to help:

* **Project Structure:** Begin by familiarizing yourself with the code layout (canonical map: [`docs/PROJECT_STRUCTURE.md`](./docs/PROJECT_STRUCTURE.md); architecture hub: [`docs/architecture.md`](./docs/architecture.md)). At a glance:

  * `Exports/` – core libraries and native components: `GameData.Core` (interfaces), `BotRunner` (the shared behavior engine), `WoWSharpClient` (pure-C# WoW protocol), `BotCommLayer` (Protobuf/TCP IPC), and the C++ `Navigation` (Detour/Recast + physics), `Loader` (CLR injection), and `FastCall`.
  * `Services/` – worker services: `WoWStateManager` (orchestration + foreground injection), `ForegroundBotRunner` (injected, in-process), `BackgroundBotRunner` (headless), `PathfindingService`, `SceneDataService`, `DecisionEngineService`, `PromptHandlingService`.
  * `BotProfiles/` – per class/spec combat rotation profiles.
  * `UI/` – the WPF operator console (`WoWStateManagerUI`) and the .NET Aspire AppHost (`Systems/Systems.AppHost`).
  * `tools/` – CLI utilities, including the `MmapGen` navmesh generator.
  * `docs/` – specifications, plans, and architecture documentation.

* **Coding Guidelines:** We follow standard C# coding styles for the managed code and typical C++ practices for the injector. Ensure any new code is well-documented and tested. Because this is a hobby/research project, we value clarity and experimentation over strict style – but try to match the general structure of existing code for consistency. For example, if adding a new bot behavior, see how existing states and profiles are implemented.

* **Submitting Changes:**

  1. Fork the repository (or work on a feature branch if you have access).
  2. Create an Issue if your contribution is significant or changes behavior, to discuss with maintainers and community first. This is especially recommended for big features (e.g. “Implement group coordination AI” or “Integrate GPT-4 for chatbots”).
  3. Make your changes in a new branch, with descriptive commit messages. If fixing a bug, reference any issue number in commits.
  4. Ensure the project builds and runs after your changes. Ideally test with at least one WoW client to confirm nothing broke (e.g., the bot can still inject and move/fight).
  5. Submit a Pull Request to the **main branch**. Describe what your PR does, and any steps to test it. Include screenshots or logs if appropriate (for example, showing a new feature in action).
  6. Be patient for review. Since this is a personal/opensource project, reviews might not be immediate. Community members or maintainers might provide feedback or ask for adjustments.

* **Contribution Ideas:** Not sure what to work on? Some sought-after contributions:

  * New **Class Profiles** or improvement to existing ones (better combat rotations, support for more spells, smarter tactics).
  * **Questing AI:** enabling bots to complete quests (requires parsing quest logs, navigating to objectives, handling quest items).
  * **Social AI:** give bots the ability to respond to player interactions – e.g. chat back when spoken to, join groups, participate in trades or auctions.
  * **Advanced Navigation:** improvements to the pathfinding – maybe integrating dynamic avoidance (not walking into crowds of enemies recklessly) or using transport (fly/boat).
  * **Performance and Scalability:** optimizing the code to run more bots per machine. Perhaps allowing multiple bots in one process or headless operation with a virtual game world.
  * **Bug Fixes:** If you encounter crashes or bugs (e.g., certain abilities not working, or bot getting stuck), tracking down and fixing those is extremely valuable.
  * **Documentation:** Enhancing documentation, tutorials, or even writing research papers/blogs on experiments done with WWoW.

* **Community and Support:** We encourage you to join the conversation. You can use the project’s GitHub Issues for support questions or the discussion forum (if enabled). Additionally, the original BloogBot had a Discord server for hacking on the project – WWoW users may find support there or we might establish a dedicated WWoW Discord if interest grows. Sharing your use-cases and successes (or failures) will help shape the project’s direction!

* **License:** The project is open-source (MIT License). Any contributions must be compatible with this license. By contributing, you agree that your code will be MIT-licensed as well. This permissive license allows both academic and commercial use, as long as copyright notices are maintained.

We appreciate any form of contribution, be it code, ideas, or simply bug reports. Let’s build this AI-driven world together!

## Roadmap

WWoW is an ambitious project, and there’s a lot on the horizon. Here’s a high-level roadmap of where we plan to go next, including integration with cutting-edge AI and data platforms:

* **Short Term (Current Focus):**

  * **Stability and Core Features:** Iron out remaining bugs in basic bot behaviors (combat, navigation, staying alive). Ensure each class profile can at least level from 1 to 20 unattended as a proof of concept. Improve the database of hotspots and NPCs for more zones, so bots can travel world-wide.
  * **AI Foundry Integration (Phase 1):** Begin connecting WWoW to **Azure AI Foundry**, an Azure service for building and managing AI agents. Initially, this might involve using Foundry to manage our bot profiles or agent logic in a more modular way. For example, offloading certain decision-making processes to an AI Foundry agent that can be updated or trained externally. This could let us experiment with more advanced AI controllers without embedding everything in the game client.
  * **Data Logging with Microsoft Fabric:** Leverage **Microsoft Fabric** (Microsoft’s unified analytics platform) to collect and analyze gameplay data from WWoW. Every action an AI bot takes, every event (kill, death, item looted, interaction) could be funneled into a Fabric data pipeline. This will allow for big-data analysis of AI behavior. We plan to create dashboards and reports (using Fabric’s data warehousing and Power BI integration) to visualize how bots perform over time or identify patterns (e.g., places where bots die frequently or inefficient paths taken).
  * **Agent Behavior Enhancements:** Implement more nuanced behavior rules: for instance, making bots choose between grinding vs. questing, or having different “personalities” (aggressive vs. cautious playstyles). This may tie in with AI Foundry if we treat each bot as an agent with a profile that can be tweaked or even a learning agent updated through Azure.

* **Mid Term (Next Steps):**

  * **Conversational Bots:** Integrate NLP models (possibly via Azure OpenAI Service or similar) so that bots can engage in basic conversation. The goal is for an AI player to respond to a hello or even answer simple questions (“Where is the blacksmith?”) in a plausible way. AI Foundry could host a language model agent that WWoW bots query when they need to generate a chat response. This will significantly increase human-bot indistinguishability if done well.
  * **Group Dynamics:** Teach bots to cooperate. Using data from Fabric about how human parties tackle content, we can script or train bots to assume roles in a party (tank, healer, DPS coordination). This includes understanding threat mechanics, assisting each other, and possibly even forming ad-hoc groups with players or other bots when facing tough challenges.
  * **Event and Quest Simulation:** Expand the bot’s capabilities beyond combat: for example, participating in in-game economies (auction house auto-buying/selling using economic agents), joining PvP battlegrounds with rudimentary tactics, or dynamically generating quest-like tasks for bots to “pursue” (giving them goals beyond just grinding). This might involve integrating with game server APIs or using external logic to feed objectives to bots.
  * **Scalability & Cloud Gaming:** Investigate running larger-scale simulations. This could involve orchestrating multiple game clients in the cloud (perhaps using virtualization or a custom headless server that mimics a client). Microsoft Fabric could help coordinate this by provisioning compute for bot instances and aggregating their data. Imagine a fully automated test server with 100+ bot players populating the world – this could be used to test server load or emergent behaviors when many AI agents interact.
  * **Microsoft Fabric Data Agent:** Utilize Fabric’s Data Agent to stream data directly from the game environment (via the bots) into Fabric’s lakehouse. This real-time data could then be used to adjust agent behavior on the fly. For example, if analysis shows a particular grinding spot is overcrowded (many bots converging), an AI coordinator (maybe hosted in AI Foundry) could assign some bots to move to different areas. This begins to resemble a higher-level “AI director” managing the population of bots.

* **Long Term (Vision):**

  * **True Westworld Experience:** Achieve a state where a human can join a WWoW server and truly not tell if others are bots or people. This means polishing all aspects of bot behavior: natural movement (no bottish jittering or super-linear paths), human-like decision delays and mistakes, engaging conversation, perhaps even creative play (like occasionally doing silly things, or participating in server events). It’s a Turing Test in Azeroth.
  * **Learning Agents:** Incorporate reinforcement learning or other advanced AI that *learns* from the environment. We could use the logged data (via Fabric) to train models that optimize how bots play (for example, learning the optimal grinding spots or combat strategies). Over time, bots could become smarter and adapt to player tactics (in PvP scenarios, for instance).
  * **Cross-Platform / Other Games:** While currently built around World of Warcraft, the principles of WWoW could extend to other virtual worlds. A long-term possibility is to abstract the “Agent in MMORPG” core and apply it to different game environments (imagine Westworld-like simulations in other MMO games or open-world games). Microsoft Fabric’s analytics and Azure AI Foundry’s agent management could make it easier to plug into new worlds by swapping out environment data and retraining AI models.
  * **Community-Driven Worlds:** We hope to involve the community in hosting WWoW servers where bot and human interactions can be observed in the wild. Insights from these could drive further development. Perhaps competitions or Turing-test like events could be held, challenging players to identify bots – pushing us to improve them further.

The roadmap above is ambitious, and not set in stone. As an open-source project, progress will depend on contributions and discoveries along the way. Integrating **AI Foundry** and **Microsoft Fabric** is an exciting avenue – it brings enterprise-grade AI and data tools into the mix, which can supercharge the intelligence of our agents and our understanding of them. We especially look forward to how **agentic behavior** can evolve: from scripted state machines to autonomous, adaptive agents that truly feel “alive” in the game world.

Stay tuned for updates in the repository. If you’re interested in any of these roadmap areas, please reach out or jump in – help us build the Westworld of Warcraft!

## Getting Help and Further Information

For newcomers, the amount of moving parts (WoW clients, AI code, databases, etc.) can be daunting. Here are some resources and tips:

* **Documentation:** We are working on more documentation in the `Docs/` folder. You can find a basic FAQ and perhaps guides there (e.g., a FAQ might address common setup issues).
* **Original Project Writings:** Much of WWoW’s core is based on the BloogBot project by Drew Kestell, who wrote a series of blog posts on how it works. Those articles are a great way to understand the technical inner workings (memory hacking, pathfinding, etc.). We plan to curate and include some of that information in our documentation as well.
* **Community Forums/Discord:** As mentioned, consider joining the discussion via our (to-be-formed) Discord or via the original BloogBot Discord. There may be others out there working on similar projects or who have used BloogBot/WWoW and can share knowledge.
* **Issues on GitHub:** If you run into problems, you can search the issue tracker to see if it’s known, or open a new issue describing your situation. We (the maintainers) or community members will do our best to help.
* **Safety and Ethics:** Remember, this project is for learning and fun. Please use it responsibly. Do **not** use WWoW (or its predecessor BloogBot) to disrupt real servers or cheat in live games – not only is that against terms of service of virtually all games, but this project is intentionally limited to older clients and private settings to avoid those concerns. We encourage experimentation in controlled environments where everyone involved consents to bots being present.
* **Have Fun:** At its heart, WWoW is a labor of love merging gaming and AI. Whether you’re here to build Skynet for Azeroth or just to watch a Rogue bot foolishly try to pickpocket a dragon, we hope you enjoy the journey. Feel free to share stories of cool (or hilarious) things your bots did!

---

*Welcome to Westworld of Warcraft. Build an army of AI heroes, explore new horizons in AI-driven gameplay, and help us create a virtual world where the line between player and program blurs.*

Happy adventuring, both to the humans and the algorithms!&#x20;
