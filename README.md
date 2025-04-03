
# Terraria Pixel Art Creator (WPF)

![Terraria Logo](https://static.wikia.nocookie.net/terraria_gamepedia/images/f/ff/NewPromoLogo-3.png/revision/latest?cb=20201127171805)  
**Приложение для создания пиксель-арта, совместимого с Terraria.**

---

## 📋 О проекте
WPF-приложение, позволяющее создавать пиксельную графику для последующего импорта в игру Terraria. Позволяет обрабатывать как фото, так и видео.
Импорт осуществляется с помощью специального мода, в котором необходимо нажать на одну кнопку. 

---

## 🎯 Особенности
- **Уникальность**: Рисуйте ваши изображения прямо на карте.
- **Палитра Terraria**: Встроенные цвета, соответствующие блокам и предметам из игры.
- **Экспорт**:
  - В `.txt` для прямого импорта в Terraria.
- **Инструменты**:
  - Различные алгоритмы дотирования

---

## 🚀 Быстрый старт
### Установка
1. Клонируйте репозиторий:
   ```bash
   git clone https://github.com/MKSO4KA/TerrariaPixelArtCreatorWPF.git
   ```
2. Откройте `TerrariaPixelArtCreator.sln` в Visual Studio.
3. Соберите решение (**Build → Build Solution**).
4. Запустите приложение через `F5`.

### Требования
- Windows 10 or above
- .NET Framework 4.7.2+
- Visual Studio 2019+ (для сборки).

---

## 🖌️ Использование
1. **Выберете выходной путь**:  
   *Sttings → Browse → Выберите путь → Save Settings*
2. **При необходимости выберете FPS**
   *Target FPS → Количество FPS в вашем видео*
3. **Выберете изображение или видео на соотвествующей вкладке**:  
   *MainWindow → Browse → Выберите путь → Start Processing*
4. **Подождите окончания**:  
   *Окошко Success*

---

## 📂 Структура проекта
```
TerrariaPixelArtCreatorWPF/
├── MainWindow.xaml       # Основное окно приложения
├── App.xaml              # Стартовая точка приложения
├── CustomMessageBox.xaml # Кастомный блок сообщений
├── Dithering.cs          # Логика дотирования
├── Partial.cs            # Большие данные и мусор
├── Resources/
│   └── Palette.json      # Цветовая палитра Terraria в будущем
└── LICENSE               # Лицензия GNU GPL v3
```

---

## ❓ FAQ
### Как импортировать арт в Terraria?
1. Экспортируйте проект в `C:\Users\USER\OneDrive\Docuements\PixelArtCreatorByMixailka\photo1.txt`.
3. В игре: *Бразуер модов → PaletteCreator → Импорт →  В мир → Controls → InsertArt → YourKeybind → Save → TapOnIt.*
---

## 📄 Лицензия
Распространяется под лицензией **GNU GPL v3**. Подробнее — в файле [LICENSE](LICENSE).

---

## 🤝 Участие в разработке
1. Форкните репозиторий.
2. Создайте ветку:  
   ```bash
   git checkout -b feature/your-idea
   ```
3. Зафиксируйте изменения:  
   ```bash
   git commit -m "Добавлено: Ваша фича"
   ```
4. Отправьте изменения:  
   ```bash
   git push origin feature/your-idea
   ```
5. Создайте **Pull Request**.

---

## 📌 Планы
- [x] Переместить дефолтную палитру в json файл 13.03.2025
- [x] Сделать хорошее древо файлов 13.03.2025
- [ ] Бета-тест beta ветки
- [ ] Сделать конфиг у мода в Tmodloader
- [ ] Нейро выбор алгоритма дотирования
- [ ] Редактор самих артов
- [ ] Undo/Redo система.
- [ ] Поддержка слоев.
- [ ] Импорт спрайтов из игры.

---

## 🛠️ Проект на паузе

---

## 📬 Контакты
Автор: **[MKSO4KA](https://github.com/MKSO4KA)**  
Почта: **mixailkaforpda@gmail.com**  
Issues: [Сообщить о проблеме](https://github.com/MKSO4KA/TerrariaPixelArtCreatorWPF/issues)
