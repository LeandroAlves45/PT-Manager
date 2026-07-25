#!/usr/bin/env python3
"""
Graphify Pseudocode Generator
Generates extended pseudocode for Chatbot project components following Clean Architecture.
"""

import json
import os
import re
from pathlib import Path
from typing import Optional, Dict, List
from datetime import datetime


class PseudocodeGenerator:
    """Generates structured pseudocode from project graph and golden rules."""
    
    def __init__(self, project_root: str):
        """Initialize with project root directory."""
        self.project_root = Path(project_root)
        self.graph_path = self.project_root / "graphify-out" / "graph.json"
        self.report_path = self.project_root / "graphify-out" / "GRAPH_REPORT.md"
        self.claude_md_path = self.project_root / "claude.md"
        
        self.graph_data = None
        self.golden_rules = {}
        self.load_graph()
        self.load_golden_rules()
    
    def load_graph(self):
        """Load and parse the graph.json file."""
        if self.graph_path.exists():
            with open(self.graph_path, 'r', encoding='utf-8') as f:
                self.graph_data = json.load(f)
        else:
            raise FileNotFoundError(f"Graph not found at {self.graph_path}. Run 'graphify . --code-only' first.")
    
    def load_golden_rules(self):
        """Extract golden rules from claude.md."""
        if self.claude_md_path.exists():
            with open(self.claude_md_path, 'r', encoding='utf-8') as f:
                content = f.read()
                # Parse golden rules section
                if "Golden Rules:" in content or "golden Rules" in content:
                    self.golden_rules = self._parse_rules(content)
    
    def _parse_rules(self, content: str) -> Dict:
        """Parse golden rules from markdown content."""
        rules = {
            "architecture": "Clean Architecture",
            "language_methods": "English",
            "language_comments": "Portuguese (PT-PT)",
            "di_pattern": "Dependency Injection",
            "result_pattern": "Result<T>",
            "testing_framework": "xUnit",
            "coverage_target": "80%+",
        }
        return rules
    
    def find_dependencies(self, feature_name: str) -> List[str]:
        """Find connected nodes in graph for a feature."""
        if not self.graph_data or "nodes" not in self.graph_data:
            return []
        
        dependencies = []
        feature_lower = feature_name.lower()
        
        # Find nodes that contain the feature name
        for node in self.graph_data.get("nodes", []):
            node_name = node.get("id", "").lower()
            if feature_lower in node_name:
                # Find connected edges
                for edge in self.graph_data.get("edges", []):
                    if edge.get("source", "").lower() == node_name or edge.get("target", "").lower() == node_name:
                        other = edge.get("target") if edge.get("source", "").lower() == node_name else edge.get("source")
                        if other and other not in dependencies:
                            dependencies.append(other)
        
        return dependencies[:10]  # Limit to 10 most relevant
    
    def generate_pseudocode(
        self,
        feature: str,
        context: str,
        learning_focus: str,
        dependencies: List[str],
        additional_context: Optional[str] = None
    ) -> str:
        """Generate extended pseudocode for a feature."""
        
        # Extract namespace from context
        namespace_map = {
            "Application.UseCases": "Application.UseCases",
            "Domain.Entities": "Domain.Entities",
            "Infrastructure.Repositories": "Infrastructure.Repositories",
            "WebApi.Endpoints": "WebApi.Endpoints",
        }
        namespace = namespace_map.get(context, context)
        
        pseudocode = self._generate_header(feature, context, namespace)
        pseudocode += self._generate_objective(feature, context)
        pseudocode += self._generate_mentor_note(feature, learning_focus)
        pseudocode += self._generate_xml_doc_section(feature, context)
        pseudocode += self._generate_pseudocode_section(feature, context, learning_focus, namespace)
        pseudocode += self._generate_implementation_notes(learning_focus)
        pseudocode += self._generate_checklist(feature, context)
        pseudocode += self._generate_next_steps()
        
        return pseudocode
    
    def _generate_header(self, feature: str, context: str, namespace: str) -> str:
        """Generate markdown header."""
        return f"""# {feature} — Implementation Guide

**Context:** {context}  
**Namespace:** `{namespace}`  
**Generated:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}  

---

"""
    
    def _generate_objective(self, feature: str, context: str) -> str:
        """Generate objective section."""
        objectives = {
            "Application.UseCases": f"Orchestrates the business logic for {feature}, coordinating repositories and services. Returns always a Result<T>, never throwing exceptions for business flows.",
            "Domain.Entities": f"Models the core business entity {feature}. Contains pure business logic, no external dependencies. Identity-based equality.",
            "Infrastructure.Repositories": f"Persists and retrieves {feature} from the database using EF Core. Implements the repository interface from Domain.",
            "WebApi.Endpoints": f"Exposes HTTP contract for {feature}. Delegates to use cases, never contains business logic.",
        }
        
        objective = objectives.get(context, f"Implements {feature} following Clean Architecture principles.")
        
        return f"""## Objective

{objective}

"""
    
    def _generate_mentor_note(self, feature: str, learning_focus: str) -> str:
        """Generate mentor note section."""
        return f"""## Why This Matters (mentor note)

This component teaches: **{learning_focus}**

When you understand this pattern, you'll see how the layers communicate without tight coupling. Focus on:
- How dependencies flow (always from outer layers → inner layers)
- Why we use Result<T> instead of exceptions for business flows
- How Domain stays testable without mocks
- Why we persist atomically, not piece by piece

Gotcha: It's tempting to skip steps or hardcode. Don't. The pattern exists because it scales and makes testing real.

---

"""
    
    def _generate_xml_doc_section(self, feature: str, context: str) -> str:
        """Generate XML doc structure section."""
        if "UseCase" in feature:
            return f"""## XML Documentation Structure

For C# classes, use this exact structure:

```csharp
namespace Application.UseCases;

/// <summary>
/// [Single line describing what this use case does and what it returns.]
/// </summary>
/// <remarks>
/// This use case orchestrates [components], ensuring [business rule/constraint].
/// Throws <see cref="[ExceptionType]"/> only if [specific external condition].
/// Never throws for normal business flows — uses Result<T> instead.
/// </remarks>
public class {feature}
{{
    // Constructor XML doc is omitted — DI pattern is self-explanatory
    // Method XML doc: only if behavior is non-obvious
}}
```

**Guidelines:**
- `<summary>`: One sentence, active voice, what → output
- `<remarks>`: Why, constraints, exception policy
- No XML doc on obvious getters/setters
- Inline comments explain WHY, not WHAT

"""
        else:
            return f"""## XML Documentation Structure

For C# classes, use this exact structure:

```csharp
namespace {context};

/// <summary>
/// [Single line describing responsibility and purpose.]
/// </summary>
public class {feature}
{{
    // Inline comments explain WHY decisions, not WHAT code does
}}
```

**Guidelines:**
- `<summary>`: One sentence, clear intent
- Comments in Portuguese explain technical decisions
- Methods in English, comments in Portuguese

"""
    
    def _generate_pseudocode_section(self, feature: str, context: str, learning_focus: str, namespace: str) -> str:
        """Generate main pseudocode section."""
        
        pseudocode = f"""## Pseudocode: {feature}

```
namespace {namespace}

// <summary>{feature} — [brief responsibility]</summary>
CLASSE {feature}

    CAMPO PRIVADO SÓ-LEITURA _[dependency1] (tipo [IInterface])
    CAMPO PRIVADO SÓ-LEITURA _[dependency2] (tipo [IInterface])

    CONSTRUTOR([dependency1]: [IInterface], [dependency2]: [IInterface])
        ATRIBUI campos para uso posterior
        
        // WHY: Dependency Injection permite testar sem dependências reais
        // e desacopla esta classe de implementações concretas.

    MÉTODO ASYNC ExecuteAsync([request]: [RequestType]) RETORNA Task<Result<[ResponseType]>>
    
        // Início da lógica de negócio
        DEFINE [entity] = nova instância ou recuperada do repositório
        
        SE [validação] ENTÃO
            DEVOLVE Result<[ResponseType]>.Failure("[mensagem de erro]")
        
        // Operação de negócio principal
        [entity].[operação]([parâmetros])
        
        // WHY: Chamamos SaveChangesAsync uma única vez no final
        // para garantir atomicidade — ambas as mudanças são gravadas
        // numa mesma transação ou nenhuma é gravada.
        AGUARDA _repository.SaveChangesAsync()
        
        // Mapear e devolver resultado
        DEFINE response = novo [ResponseType](...)
        DEVOLVE Result<[ResponseType]>.Success(response)

    FIM MÉTODO

FIM CLASSE
```

**Mapping pseudocode to real code:**
- `CLASSE` → `public class`
- `CAMPO PRIVADO SÓ-LEITURA` → `private readonly`
- `MÉTODO ASYNC` → `public async Task<>`
- `AGUARDA` → `await`
- `DEVOLVE` → `return`
- `SE ... ENTÃO` → `if () {{}}`
- `DEFINE` → variable assignment

**Comments explain:**
- WHY we do it (technical reason, not what code does)
- WHEN (under what conditions)
- GOTCHAS (edge cases, performance, security)

"""
        return pseudocode
    
    def _generate_implementation_notes(self, learning_focus: str) -> str:
        """Generate implementation notes."""
        return f"""## Implementation Notes

### Comment Style
- **What NOT to write:** `// Create a conversation` (code is obvious)
- **What TO write:** `// WHY: Single SaveChangesAsync at end ensures atomicity`
- **Language:** Portuguese (PT-PT), explains decisions and gotchas

### Error Handling
- Domain/Application layers: Use Result<T> for business failures
- WebApi layer: Middleware catches unexpected exceptions → 500 responses
- Never throw for recoverable business flows

### Testing Implications
- **Domain**: No mocks, pure functions, test business logic
- **Application**: Mock repositories and services, test orchestration
- **Infrastructure**: Integration tests with real database (or SqliteInMemory)
- Target 80%+ coverage; focus on logic paths, not line-by-line

### Dependency Injection Pattern
- Constructor always receives interfaces (IRepository, IService)
- Never instantiate dependencies directly
- Enables testing by passing mocks

### Focus Areas for This Component
**{learning_focus}**

- Understand each line's purpose
- Run tests after implementation
- Compare your code to `05-use-cases.md` pattern

"""
    
    def _generate_checklist(self, feature: str, context: str) -> str:
        """Generate implementation checklist."""
        return f"""## Checklist

- [ ] Namespace and file location correct (`{context}`)
- [ ] All constructor dependencies injected (no `new` keyword)
- [ ] XML doc summary written (one sentence, clear intent)
- [ ] WHY comments added for non-obvious decisions
- [ ] Error paths use Result<T>.Failure, not exceptions
- [ ] SaveChangesAsync/database operations batched atomically
- [ ] No hardcoded credentials or secrets
- [ ] Code compiles and builds without errors
- [ ] Unit tests written (Domain: 100%, Application: mocked dependencies)
- [ ] Feature branch follows `feat/` convention
- [ ] Commit message follows conventional commits

"""
    
    def _generate_next_steps(self) -> str:
        """Generate next steps section."""
        return """## Next Steps

1. **Copy the pseudocode** into your editor as a guide
2. **Implement following the structure** — class definition, constructor, methods
3. **Add XML doc and WHY comments** as you write
4. **Write tests alongside** (test-driven development)
5. **Verify dependencies** — ensure you're not reaching across layers
6. **Reference `05-use-cases.md`** for similar patterns already implemented

---

**Questions?** Check:
- `architecture.md` — Overall structure
- `clean-architecture-guide.md` — Layer responsibilities
- `security-conventions.md` — Security patterns
- `database-schema.md` — Database design
"""
    
    def save_pseudocode(self, pseudocode: str, file_location: str) -> Path:
        """Save pseudocode to file."""
        output_path = self.project_root / file_location
        output_path.parent.mkdir(parents=True, exist_ok=True)
        
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(pseudocode)
        
        return output_path


def main():
    """Example usage."""
    project_root = "C:\\Users\\Leandro Alves\\Desktop\\Projetos\\Chatbot Project\\Chatbot-project\\Chatbot"
    
    try:
        generator = PseudocodeGenerator(project_root)
        
        pseudocode = generator.generate_pseudocode(
            feature="CreateConversationUseCase",
            context="Application.UseCases",
            learning_focus="Dependency Injection + Result pattern",
            dependencies=["IConversationRepository"],
            additional_context="Basic use case with no error paths"
        )
        
        output_file = generator.save_pseudocode(
            pseudocode,
            "docs/pseudocode/CreateConversationUseCase.md"
        )
        
        print(f"✓ Pseudocode generated: {output_file}")
        
    except Exception as e:
        print(f"✗ Error: {e}")


if __name__ == "__main__":
    main()
