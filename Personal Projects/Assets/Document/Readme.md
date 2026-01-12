### 1 Start from Interface not concrete MonoBehaviour
- Behavior over concrete
  - class depend on behavior, not concrete type
- Easy to swap/replace
  - swapping or extending functionality doesn't require rewriting everything
- Easy to test
  - logic becomes easier to test outside of play mode
- Easy to reuse
  - systems become reusable instead of one-off

### 2 Separating Logic From MonoBehaviour
- Behavior without unity
  - core behavior can run without Unity
- Logic easier to test
  - logic becomes trivially to test
- Separating of concerns
  - monobehavior can focus on lifecycle and scene integration and not decision making

### 3 Separating Data
- No code changes
  - balance changes don't require code changes
- Behavior-focused logic
  - logic stay focused on behavior
- Safe value tweaks
  - designers can tweak values safely and test become even more clear and explicit

### 4 Event Driven
- Clear flow of intent
- Less conditional logic
  - fewer conditionals in gameplay code
- Behavior is reusable
  - much easier to reuse the same behavior in different contexts
- Easier to extend
  - like player input, ai, ui, networking

### 5 Registry
- No search or find calls
  - no more FindObjectOfType or similar calls
- No direct references
  - no more references everywhere
- Shared view of "what exist now"
  - systems can query what exist without direct references
- Extensible
  - new systems can be added without changing existing systems

###### reference Youtube: git-amend