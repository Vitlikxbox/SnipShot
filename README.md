<div align="center">

# 📸 SnipShot

**Скриншот области по горячей клавише: сразу и в буфере обмена, и в папке**

![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white)
![C#](https://img.shields.io/badge/C%23-.NET%20Framework%204-512BD4?logo=dotnet&logoColor=white)
![No install](https://img.shields.io/badge/установка-не%20нужна-success)
![License](https://img.shields.io/badge/license-MIT-blue)

<img src="docs/screenshot.png" alt="Скриншот" width="382">

</div>

## ✨ Возможности

- ✂️ `Ctrl+Alt+A`: выдели мышью любую область, рядом показывается её размер в пикселях
- 🖥️ `Ctrl+Alt+S`: весь экран (тот, где сейчас курсор). `Ctrl+Alt+W`: активное окно без тени
- 📋 Снимок сразу в буфере обмена, вставляй `Ctrl+V` в чат или документ
- 💾 И одновременно сохраняется PNG с датой в имени (по умолчанию `Изображения\SnipShot`)
- 🖱️ Esc или правый клик отменяют выделение. Во время выделения экран «замораживается», так что можно снять и всплывающие меню
- 🖥️🖥️ Работает с несколькими мониторами и высоким DPI. Живёт в трее

## 💡 Когда пригодится

- Быстро показать ошибку коллеге или в поддержку
- Сохранить кусок переписки, чек или расписание
- Делаешь инструкцию и нужно много однотипных скриншотов подряд

<div align="center"><img src="docs/region.png" alt="Выделение области" width="520"></div>

## 📥 Установка

1. Скачай `SnipShot.exe` из раздела [**Releases**](../../releases/latest)
2. Запусти его, установка не нужна

## 🛠️ Сборка из исходников

Нужен только Windows: компилятор C# входит в состав .NET Framework.

```bat
build.bat
```

## 📄 Лицензия

[MIT](LICENSE)
