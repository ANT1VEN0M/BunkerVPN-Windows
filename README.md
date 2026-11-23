# 🐈‍⬛ BunkerVPN for Windows PC (10 / 11)

<p align="center">
  <img src="src/BunkerVPN/Resources/Icons/app_icon.png" width="128" height="128" alt="BunkerVPN Logo" />
</p>

<p align="center">
  <b>Официальный высокоскоростной клиент обхода блокировок для компьютеров и ноутбуков на Windows 10 / 11.</b><br>
  Разработано сообществом <b>Бункер Кота 🐾 (1400+ подписчиков)</b>.
</p>

<p align="center">
  <a href="https://github.com/ANT1VEN0M/BunkerVPN-Windows/releases/latest"><img src="https://img.shields.io/github/v/release/ANT1VEN0M/BunkerVPN-Windows?color=FF6D00&label=%D0%A0%D0%B5%D0%BB%D0%B8%D0%B7_v1.0.6" alt="Release" /></a>
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-00E5FF" alt="Windows" />
  <img src="https://img.shields.io/badge/.NET-8.0%20WPF-7C4DFF" alt=".NET 8" />
  <img src="https://img.shields.io/badge/Core-sing--box%201.11.4-00E676" alt="Sing-box" />
  <a href="https://t.me/BUNKER_KOTA"><img src="https://img.shields.io/badge/👥_Аудитория-1400+_подписчиков-0088cc?style=flat-square&logo=telegram&logoColor=white" alt="1400 Subscribers" /></a>
  <a href="https://t.me/BUNKER_K0TA"><img src="https://img.shields.io/badge/💬_Чат-BUNKER__K0TA-7C3AED?style=flat-square&logo=telegram&logoColor=white" alt="Telegram Chat" /></a>
</p>

---

## ⚡ Главные возможности (Версия v1.0.6)

* 🚀 **TUN-режим (Глобальный VPN для всего ПК):**
  Маршрутизация трафика всей операционной системы через виртуальный высокоскоростной сетевой драйвер **WinTUN**. Работает для браузеров, онлайн-игр, Steam, торрентов и всех фоновых служб.
* 🎮 **Discord Voice & Full-Cone UDP:**
  Прямая поддержка UDP-инкапсуляции и WireGuard WARP+ (порт 880) для мгновенного подключения к голосовым каналам и стримам Discord без зависания на «RTC Connecting».
* 🛡️ **Виджет статуса сервисов (ТСПУ Bypass Grid):**
  Интерактивная панель разблокировки на главном экране в реальном времени отображает состояние ключевых платформ: **YouTube 4K**, **Discord Voice**, **Telegram SOS**, **AI (ChatGPT / Claude)**, **Instagram / X**, **Банки и Госуслуги (Smart Bypass)**.
* 📶 **Интеллектуальная автосортировка по пингу:**
  Приложение автоматически замеряет задержку серверов в фоне и поднимает самые быстрые и стабильные узлы наверх списка. Доступна кнопка ручной пересортировки в 1 клик.
* 🌐 **URL Тест (HTTP 204 Real Check):**
  Реальный тест сквозного прохождения интернет-трафика через сервер без лишних задержек.
* 🛡️ **Умный обход (Smart Bypass Russia):**
  Российские сервисы (Яндекс, Госуслуги, VK, Сбер, Т-Банк, Кинопоиск, маркетплейсы) открываются напрямую на полной скорости вашего домашнего провайдера, а заблокированные ресурсы — через защищённый туннель Бункера.
* 🔄 **Встроенный 1-Click Автообновлятор:**
  Фоновая проверка новых версий и бесшовная установка свежих релизов прямо из интерфейса программы.
* 🔔 **Интеграция с треем Windows и автозапуск:**
  Приложение сворачивается в системный трей возле часов, потребляет минимум оперативной памяти (~20 МБ) и позволяет управлять туннелем в один клик.
* 🌐 **Облачный веб-десктоп (noVNC):**
  Возможность запускать и тестировать ПК-версию прямо в браузере смартфона или планшета через встроенный workflow GitHub Actions.

---

## 📦 Скачать BunkerVPN v1.0.6

| Тип загрузки | Прямая ссылка (GitHub) | Зеркало для РФ (без VPN) | Описание |
| :--- | :---: | :---: | :--- |
| 📥 **Установщик (Setup.exe)** | [**Скачать Setup v1.0.6**](https://github.com/ANT1VEN0M/BunkerVPN-Windows/releases/download/v1.0.6/BunkerVPN-Windows-v1.0.6-Setup.exe) | [**Зеркало РФ (Setup)**](https://ghfast.top/https://github.com/ANT1VEN0M/BunkerVPN-Windows/releases/download/v1.0.6/BunkerVPN-Windows-v1.0.6-Setup.exe) | Рекомендуется. Устанавливает программу, создаёт ярлыки на рабочем столе и автозапуск. |
| 🧰 **Портативная версия (ZIP)** | [**Скачать Portable v1.0.6**](https://github.com/ANT1VEN0M/BunkerVPN-Windows/releases/download/v1.0.6/BunkerVPN-Windows-v1.0.6-Portable.zip) | [**Зеркало РФ (Portable)**](https://ghfast.top/https://github.com/ANT1VEN0M/BunkerVPN-Windows/releases/download/v1.0.6/BunkerVPN-Windows-v1.0.6-Portable.zip) | Не требует установки. Распакуйте архив в любую папку и запустите `BunkerVPN.exe`. |

---

## 🚀 Быстрый старт

1. Скачайте установщик `BunkerVPN-Windows-v1.0.6-Setup.exe` (или распакуйте Portable ZIP).
2. Запустите приложение (при запросе UAC разрешите запуск от имени администратора для работы драйвера WinTUN).
3. Нажмите большую оранжевую кнопку **«ПОДКЛЮЧИТЬ»**.
4. Проверьте задержку серверов на вкладке **«Серверы»** или нажмите **«📶 По пингу»** для автосортировки лучших узлов.

---

## 👑 Сообщество Бункера Кота

- 📢 **Telegram-канал:** [@BUNKER_KOTA](https://t.me/BUNKER_KOTA) *(1400+ подписчиков)*
- 💬 **Чат сообщества:** [@BUNKER_K0TA](https://t.me/BUNKER_K0TA)
- 👑 **Создатель:** [@k0tlux](https://t.me/k0tlux)
- 🤖 **Android-клиент (v1.4.1):** [BunkerVPN для Android](https://github.com/ANT1VEN0M/BunkerVPN)
- 🌐 **Официальный веб-портал:** [Bunker Auto-Engine](https://ant1ven0m.github.io/Bunker-Auto-Engine/)
- 🔌 **Telegram-плагины:** [Bunker Neon Loader & Chat Translator](https://github.com/ANT1VEN0M/Bunker-Auto-Engine/tree/main/plugins)
