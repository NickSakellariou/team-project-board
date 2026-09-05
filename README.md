# Team Project Board

A real-time Kanban project management application for small teams.

This project is being built as a personal learning project to explore modern .NET application architecture, real-time collaboration, PostgreSQL, and frontend state management.

## Features

The MVP focuses on:

- User accounts and project memberships
- Role-based permissions
- Team project management
- Kanban boards, columns, and tasks
- Moving and reordering tasks
- Real-time board updates between connected users

## Architecture

The application uses a **Modular Monolith** architecture with **Clean Architecture** and **Vertical Slices**.

### Modular Monolith

The application is deployed as a single application while keeping business domains separated into independent modules.

This provides clear boundaries without introducing the operational complexity of microservices for a small project.

### Clean Architecture + Vertical Slices

Clean Architecture is used to keep the domain independent from infrastructure concerns and to enforce dependency inversion.

Vertical Slices organize the application around features rather than technical layers. For example, functionality related to creating or moving a task is kept within its corresponding feature slice.

## Tech Stack

| Area | Technology |
|---|---|
| Backend | ASP.NET Core 10 Web API |
| API Style | Minimal APIs |
| Architecture | Modular Monolith + Clean Architecture + Vertical Slices |
| Real-time Communication | SignalR |
| Frontend | React + TypeScript |
| Database | PostgreSQL |