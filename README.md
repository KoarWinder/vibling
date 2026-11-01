# Vibling

A tiny pixel pet for your Windows desktop. It walks along the taskbar, climbs onto windows, plays ball, sits down at a laptop while you type and naps when bored.

[Русская версия ниже](#вайблинг)

![Vibling](docs/settings-en.png)

## Features

- Lands on window title bars, rides along when you move a window, jumps between windows.
- Can be grabbed and thrown: bounces off screen edges, lands in different ways, sometimes opens an umbrella.
- Taps a laptop while you type. Key presses are only counted, never recorded.
- Chases a ball you can throw around.
- 9 skins, 8 accessories, 4 sizes, 3 personalities.
- English and Russian.

## Controls

| Action | Result |
| --- | --- |
| Click | Pet |
| Double-click | Flip |
| Drag and release | Throw |
| Right-click / tray icon | Menu |

## Install

Download `Vibling.exe` from [Releases](../../releases) and run it. Requires Windows 10/11 and [.NET Desktop Runtime 7+](https://dotnet.microsoft.com/download/dotnet).

Build from source:

```
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o out
```

## License

[MIT](LICENSE)

---

# Вайблинг

Маленький пиксельный питомец для рабочего стола Windows. Гуляет по панели задач, забирается на окна, играет в мячик, садится за ноутбук, пока вы печатаете, и засыпает от скуки.

![Вайблинг](docs/settings-ru.png)

## Возможности

- Приземляется на верхнюю полоску окон, едет вместе с окном, прыгает с окна на окно.
- Его можно схватить и бросить: отскакивает от краёв экрана, приземляется по-разному, иногда раскрывает зонтик.
- Стучит по ноутбуку, пока вы печатаете. Нажатия только считаются, сами клавиши не сохраняются.
- Бегает за мячиком.
- 9 скинов, 8 аксессуаров, 4 размера, 3 характера.
- Русский и английский язык.

## Управление

| Действие | Результат |
| --- | --- |
| Клик | Погладить |
| Двойной клик | Сальто |
| Схватить и отпустить | Бросить |
| Правый клик / иконка в трее | Меню |

## Установка

Скачайте `Vibling.exe` из [Releases](../../releases) и запустите. Нужны Windows 10/11 и [.NET Desktop Runtime 7+](https://dotnet.microsoft.com/download/dotnet).

## Лицензия

[MIT](LICENSE)
