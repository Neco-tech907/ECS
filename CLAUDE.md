# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Что это за проект

Учебный проект на Unity DOTS/ECS. На момент создания этого файла в `Assets/` нет ни одного
собственного скрипта — это чистый шаблон **Universal 3D (URP)** с доустановленными пакетами
Entities. Вся архитектура ещё впереди, поэтому ниже описаны не существующие модули, а рамки,
внутри которых проект должен быть построен.

Тема проекта ещё не выбрана (это пункт 1 задания) и подлежит согласованию с преподавателем.

## Требования задания (определяют все архитектурные решения)

Идея проекта обязана оправдывать следующий набор — это не пожелания, а критерии приёмки:

- **Тысячи и десятки тысяч объектов** с однотипной логикой (например, волны противников).
- **Массовый спавн и удаление** этих объектов в рантайме.
- **3D на URP.**
- **Элемент случайности** (разброс времени перезарядки, порядок выхода волн, случайные точки патруля).
- **События на коллизиях/триггерах** (вход в триггер запускает волну, столкновение наносит урон).
- **Поиск пути** для части объектов.
- **Сложное поведение**: минимум 3 состояния у сущности (идти к цели / атаковать / умирать).
- **Анимации у массовых объектов** — то есть у тех же тысяч противников.

Отдельные пункты задания, которые надо реализовать явно:

- Логика движения entity к своей цели через системы и компоненты.
- Компонент(ы) характеристик entity и системы, их обслуживающие. Примеры из задания: здоровье
  с системой, реагирующей на `<= 0`; стамина, ограничивающая перемещение и плавно
  восстанавливающаяся со временем.

Ключевое следствие: «десятки тысяч объектов + анимации + поиск пути» практически исключает
классический GameObject-подход. Логика должна жить в ECS (`ISystem` + `IComponentData`),
быть Burst-компилируемой и по возможности джобифицированной. GameObject-сцена нужна только
как authoring-слой, из которого бейкеры делают entity.

## Стек

| | |
|---|---|
| Unity | 6000.3.10f1 |
| Рендер | URP 17.3.0 (`Assets/Settings/`, отдельные PC_/Mobile_ RPAsset и Renderer) |
| ECS | Entities 1.4.8, Entities Graphics 1.4.21 |
| Под капотом ECS | Burst 1.8.29, Collections 2.6.8, Mathematics 1.3.3 |
| Ввод | Input System 1.18.0, `activeInputHandler: 1` — старый Input Manager отключён |
| Навигация | AI Navigation 2.0.10 |
| Тесты | Test Framework 1.6.0 |
| Прочее | Timeline, Visual Scripting, uGUI, Newtonsoft JSON |

Чего **нет** и что придётся добавить осознанно:

- **Unity Physics / Havok (DOTS-физика) не установлены.** Стоят только классические модули
  `com.unity.modules.physics`. Коллизии и триггеры из требований на стороне ECS не заработают
  сами — нужно либо ставить `com.unity.physics`, либо писать собственные проверки
  (spatial hash / grid) в системах.
- **NavMesh-агентов в ECS нет.** `com.unity.ai.navigation` — это GameObject-слой. Для тысяч
  сущностей штатный `NavMeshAgent` не потянет; типовые варианты — flow field, собственный A*
  на джобах или Burst-совместимый `NavMeshQuery`.
- **Asmdef-файлов нет.** Весь код пока попадает в `Assembly-CSharp`. Для ECS это работает, но
  при росте проекта стоит завести асмдефы.

## Команды

Сборки и тесты запускаются либо из редактора, либо через Unity CLI в batchmode. Скриптов-обёрток
в репозитории нет. Путь к редактору:

```
C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe
```

Тесты (Unity закрывает проект, поэтому редактор должен быть выключен):

```powershell
# EditMode
& "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -runTests -batchmode `
  -projectPath "C:\Github\ESC-Project\ESC-Project" `
  -testPlatform EditMode -testResults "$env:TEMP\editmode.xml"

# PlayMode
& "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -runTests -batchmode `
  -projectPath "C:\Github\ESC-Project\ESC-Project" `
  -testPlatform PlayMode -testResults "$env:TEMP\playmode.xml"
```

Один тест или группа — через фильтр:

```powershell
-testFilter "Namespace.ClassName.TestName"
```

Собственных тестов в проекте пока нет.

## Работа через MCP for Unity

К проекту подключён MCP-сервер **UnityMCP** (HTTP, `http://127.0.0.1:8080/mcp`). Он даёт прямой
доступ к живому редактору — это основной способ проверять изменения, не выходя из сессии.

Перед тем как что-то делать, стоит прочитать состояние редактора:

- `mcpforunity://editor/state` — идёт ли компиляция, домен-релоад, play mode; поле
  `data.advice.ready_for_tools` говорит, можно ли дёргать инструменты прямо сейчас.
- `mcpforunity://project/info`, `mcpforunity://scene/...` — проект и сцена.

Дальше — `read_console` для ошибок компиляции, `manage_gameobject` / `manage_scene` /
`manage_prefabs` для правок, `run_tests` для тестов, `manage_editor` для play/pause/stop.
Ресурсы адресуются по URI (`mcpforunity://editor/state`), а не по имени (`editor_state`).

Сервер живёт отдельным python-процессом, который Unity поднимает через `uvx`. Если инструменты
отвечают `No Unity Editor instances found` — мост в редакторе выключен:
`Window → MCP for Unity → Start Bridge`. Автостарт (`MCPForUnity.AutoStartOnLoad`) не включён,
так что после перезапуска Unity мост может потребовать ручного старта.

Важно: пакет MCP for Unity при старте редактора **перезаписывает свою регистрацию** в конфиге
Claude Code, приводя её к текущему транспорту (по умолчанию HTTP). Ручные правки
`claude mcp add` для `UnityMCP` будут снесены при следующем запуске Unity — транспорт меняется
в окне MCP for Unity, а не в CLI.

## Структура

```
Assets/
  Scenes/SampleScene.unity        активная сцена
  Settings/                       URP-ассеты: PC_/Mobile_ RPAsset + Renderer, Volume-профили
  InputSystem_Actions.inputactions
  TutorialInfo/                   остатки шаблона URP (Readme.asset и его редактор) — можно удалить
Packages/manifest.json            зависимости
ProjectSettings/                  в т.ч. EntitiesClientSettings.asset
```

`Library/`, `Temp/`, `Logs/`, `obj/`, `*.csproj`, `*.slnx` — генерируемые Unity, править их
бессмысленно.

Репозиторий **не под git** и `.gitignore` отсутствует. Если git будет инициализирован, шаблон
Unity-игнора нужно добавить до первого коммита, иначе в историю уедет многогигабайтная `Library/`.

## Конвенции ECS

При написании кода держаться того, что требуют масштабы задания:

- Логика — в `ISystem` с `[BurstCompile]`, данные — в `IComponentData` (unmanaged).
  `SystemBase`/managed-компоненты — только там, где без них никак.
- Authoring-класс (`MonoBehaviour`) + `Baker<T>` для превращения префабов в entity-прототипы.
  Массовый спавн — через `EntityCommandBuffer` и `Instantiate` от запечённого прототипа.
- Состояния (идти/атаковать/умирать) выражать тегами-компонентами или enum-компонентом, а
  переходы — отдельными системами, а не ветвлением в одном большом апдейте.
- Случайность в джобах — `Unity.Mathematics.Random` с уникальным seed на entity.
  `UnityEngine.Random` в Burst-коде недоступен.
- Удаление тысяч сущностей — тоже через `EntityCommandBuffer`, не поштучно в основном потоке.
