# BotChar — Telegram Notes Bot

**BotChar** is a Telegram bot for managing personal notes with reminders.  
The project is built using **.NET 8**, **Telegram.Bot v22.7.2**, **EF Core**, and **SQLite**.

---
## Features
**Create Notes**  
The bot guides the user step by step to enter:
  The bot guides the user step by step to enter:  
  1.  Date (`DD.MM.YYYY`)  
  2. Time (`HH:MM`)  
  3. Note text  
  After that, the bot confirms the note creation considering the user's timezone.

 **View Notes**  
  Menu buttons allow the user to view notes:  
  - For the past week  
  - For the past month  
  - Custom period (user enters number of days)  

 **Delete Notes**  
  Users can delete notes via an easy-to-use menu with buttons.

 **Timezone Support**  
  All reminders are adjusted to the user's timezone.

 **Reminders**  
  The bot automatically sends reminders at the specified time, even after a restart.

 **User-friendly Inline Keyboard Menu**  
  Navigate through all bot features conveniently.

## Technologies

- **.NET 8**
- **C#**
- **Telegram.Bot v22.7.2**
- **EF Core 9 + SQLite**
- **FSM (Finite State Machine)** for sequential note creation
- **Dependency Injection** using `IServiceCollection`
- **Asynchronous update and notification handling**
