# Contributing to AI-Based Garage Management System

Thank you for your interest in contributing! This document provides guidelines for contributing to this project.

## 🚀 Getting Started

1. **Fork the repository**
2. **Clone your fork**
   ```bash
   git clone https://github.com/your-username/AI_based_garage.git
   ```
3. **Create a branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

## 💻 Development Setup

1. **Prerequisites**
   - .NET 8.0 SDK
   - SQL Server or SQL Server Express
   - Visual Studio 2022 (recommended)

2. **Install dependencies**
   ```bash
   dotnet restore
   ```

3. **Setup database**
   ```bash
   dotnet ef database update
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

## 📝 Coding Guidelines

### C# Code Style
- Follow Microsoft's C# coding conventions
- Use meaningful variable and method names
- Add XML documentation comments for public methods
- Keep methods focused and single-purpose

### Frontend Guidelines
- Write semantic HTML
- Use responsive design principles
- Ensure accessibility compliance
- Optimize for performance

## 🧪 Testing

- Write unit tests for new features
- Ensure all tests pass before submitting PR
- Include integration tests where appropriate

```bash
# Run tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## 📋 Pull Request Process

1. **Update documentation** if needed
2. **Add tests** for new functionality
3. **Ensure CI passes** all checks
4. **Update README.md** if adding new features
5. **Link related issues** in PR description

### PR Template
```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
- [ ] Unit tests added/updated
- [ ] Manual testing completed
- [ ] All tests pass

## Checklist
- [ ] Code follows style guidelines
- [ ] Self-review completed
- [ ] Documentation updated
```

## 🐛 Bug Reports

When reporting bugs, please include:
- **Clear description** of the issue
- **Steps to reproduce** the problem
- **Expected vs actual behavior**
- **Environment details** (OS, .NET version, browser)
- **Screenshots** if applicable

## 💡 Feature Requests

For new features:
- **Describe the problem** you're trying to solve
- **Proposed solution** with details
- **Alternative solutions** considered
- **Additional context** or mockups

## 📧 Contact

- Create an issue for bugs or feature requests
- Reach out via GitHub discussions for questions
- Email: [elieserkibet@gmail.com]

## 🙏 Recognition

Contributors will be recognized in:
- README.md contributors section
- Release notes for significant contributions
- Project documentation

Thank you for contributing! 🎉