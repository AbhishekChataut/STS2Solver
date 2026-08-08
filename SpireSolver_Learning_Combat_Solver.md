# SpireSolver Learning Combat Solver

## Goal

The goal is to evolve SpireSolver from an autoplayer that chooses
actions randomly into a combat solver that **learns how to play the
current deck**, improves through repeated simulations, and carries
useful knowledge from one combat into later combats.

The intended workflow is:

1.  Enter a combat.
2.  Run roughly **400--1000 simulations**.
3.  Explore different legal decisions.
4.  Record the states, actions, and final outcomes from those
    simulations.
5.  Learn which actions tend to produce better outcomes.
6.  Use that learned knowledge to make later simulations more focused.
7.  Preserve the learned model so knowledge about the deck transfers to
    future combats.
8.  Continue refining the model as the deck and encounters change
    throughout the run.

The important idea is that the solver should not memorize one exact
combat. It should learn reusable relationships about cards, resources,
enemies, and combat situations.

------------------------------------------------------------------------

## Why Pure Random Simulation Is Limited

The current random approach is useful for generating varied combat
outcomes, but it spends simulation time inefficiently.

If a state has actions such as:

-   Play Bash on Enemy A
-   Play Strike on Enemy A
-   Play Defend
-   End Turn

a random policy continues choosing all of them without learning that
some choices consistently perform better.

With only 400--1000 complete simulations available per combat, the
solver cannot afford to explore the enormous Slay the Spire decision
tree uniformly. It needs to use previous simulations to guide later
ones.

The desired progression is therefore:

``` text
Random exploration
        ↓
Record experiences
        ↓
Learn action values
        ↓
Prefer promising actions
        ↓
Keep some exploration
        ↓
Generate better experiences
        ↓
Improve the learned model
```

------------------------------------------------------------------------

## Why an Exact State Table Is Probably the Wrong Core Approach

A traditional tabular approach might store something like:

``` text
Exact Combat State → statistics for every action
```

For example:

``` csharp
Dictionary<CombatStateKey, Dictionary<ActionKey, ActionStats>>
```

This is attractive because it is simple, but Slay the Spire has an
enormous state space.

A combat state can vary by:

-   Player HP
-   Player block
-   Energy
-   Hand contents
-   Draw pile contents and order
-   Discard pile
-   Exhaust pile
-   Card upgrades and modifications
-   Player powers
-   Enemy HP
-   Enemy block
-   Enemy intents
-   Enemy powers
-   Number and type of enemies
-   Turn number
-   Status cards
-   Generated cards
-   Random effects
-   Relics and other modifiers

Drawing and shuffling alone cause states to diverge extremely quickly.

As a result, exact combat states may rarely repeat. A table could
accumulate many entries that each have only one or two observations.

The larger problem is not merely memory usage. It is **lack of
generalization**.

If the solver learns that Bash was excellent in one state, an
exact-state table does not automatically understand that Bash might also
be excellent in a slightly different state.

------------------------------------------------------------------------

## Why Function Approximation / Neural Learning Fits the Goal

Instead of asking:

> Have I encountered this exact state before?

the solver should eventually ask:

> Based on everything I have learned, how valuable does this action
> appear in a state like this?

A neural network can act as a **function approximator**:

``` text
Q(state, action) → predicted value
```

For example:

``` text
Current state + Play Bash       → 0.81
Current state + Play Strike     → 0.63
Current state + Play Defend     → 0.70
Current state + End Turn        → 0.18
```

The network does not need a dictionary entry for every possible combat
state. It learns patterns that can generalize between related
situations.

This is especially important because the desired solver should transfer
knowledge between combats.

------------------------------------------------------------------------

# Cross-Combat Learning

A major design goal is that simulations from one combat should remain
useful in later combats.

The deck usually changes gradually during a run. Between two combats,
perhaps only one card is added, upgraded, transformed, or removed.

The monsters may be completely different, but much of the learned
information about the player's deck remains relevant.

For example, the model may gradually learn relationships such as:

-   Vulnerable increases the value of attack-heavy hands.
-   Bash tends to be more valuable before other attacks.
-   Block becomes more valuable when incoming damage is high.
-   Some cards become more valuable when energy is abundant.
-   Certain card combinations work particularly well together.
-   Some cards are usually poor choices near the end of a combat.
-   Some resources are worth preserving when survival is already likely.

The desired learning cycle is:

``` text
Combat 1
    ↓
400–1000 simulations
    ↓
Model learns about the deck
    ↓
Combat 2
    ↓
Same deck with small changes
    ↓
Existing model provides prior knowledge
    ↓
400–1000 new simulations refine it
    ↓
Combat 3
    ↓
Model starts with even more experience
```

This makes simulation work cumulative instead of throwing away all
knowledge after each encounter.

------------------------------------------------------------------------

# Hybrid Approach: Persistent Learning + Current-Combat Simulation

The neural model should not necessarily be expected to solve combat
entirely by itself.

A stronger architecture combines:

1.  **Persistent learned knowledge**
2.  **Simulation of the current combat**

Conceptually:

``` text
                 Persistent Model
                       │
                 prior knowledge
                       │
                       ▼
Current Combat → 400–1000 Simulations
                       │
                       ▼
                New Experiences
                  │         │
                  ▼         ▼
          Better current   Train model
             decisions     for future
                  │         │
                  └────┬────┘
                       ▼
                   Next Combat
```

The model provides general knowledge about the deck and combat strategy.

The simulations provide evidence about the specific current encounter.

This is useful because 400--1000 simulations are far too few to
brute-force the complete game tree, but they can be extremely valuable
when guided by knowledge accumulated from previous combats.

------------------------------------------------------------------------

# Model Actions Individually, Not Entire Turn Sequences

A major source of combinatorial explosion would be trying to enumerate
every possible sequence of cards in a turn.

For example:

``` text
Bash → Strike → Defend
Bash → Defend → Strike
Strike → Bash → Defend
Strike → Defend → Bash
Defend → Bash → Strike
...
```

The solver should **not generate these sequences ahead of time**.

Instead, treat combat as a sequence of individual decisions.

At a particular moment:

``` text
Legal actions:

Play Bash → Enemy A
Play Strike → Enemy A
Play Defend
End Turn
```

After Bash is played, the state changes and legal actions are calculated
again:

``` text
Legal actions:

Play Strike → Enemy A
Play Defend
End Turn
```

This matches the existing Autoplayer structure much more naturally.

The policy only needs to answer:

> Given the current state and the actions I can legally take right now,
> which action should I choose?

------------------------------------------------------------------------

# End Turn Is a Normal Action

Slay the Spire does not require the player to spend all available
energy.

Sometimes the best choice is deliberately ending the turn while playable
cards and energy remain.

This should not require a special search system.

Represent **End Turn** as another legal action:

``` text
Play Bash
Play Strike
Play Defend
End Turn
```

The learned model can then evaluate it exactly like another decision:

``` text
Energy remaining: 2
Enemy intends no damage

Play Strike → predicted value 0.76
Play Defend → predicted value 0.72
End Turn    → predicted value 0.81
```

If End Turn has the highest expected value, the solver ends the turn.

This adds only one additional candidate action at each decision point
and can actually terminate a branch earlier.

------------------------------------------------------------------------

# Candidate-Action Evaluation

The available cards and targets constantly change, so a fixed
neural-network output such as:

``` text
Output 0 = Strike
Output 1 = Bash
Output 2 = Defend
...
```

would be awkward.

A better approach is to evaluate each currently legal action
individually:

``` text
Q(state, candidateAction)
```

For example:

``` text
State + Bash(Target A)    → 0.82
State + Strike(Target A)  → 0.67
State + Strike(Target B)  → 0.61
State + Defend            → 0.73
State + EndTurn           → 0.25
```

Then choose among the legal actions.

This naturally supports:

-   Different hands
-   Different decks
-   Multiple enemies
-   Targeted cards
-   Untargeted cards
-   End Turn
-   Cards generated during combat
-   Future action types

------------------------------------------------------------------------

# Combat State Representation

The neural network should not receive references to the entire live game
object graph.

Instead, SpireSolver should capture a **CombatSnapshot** and encode
strategically relevant information into a compact numeric
representation.

A simple early representation might contain information such as:

## Player

``` text
Current HP / Max HP
Block
Energy
Max Energy
Strength
Dexterity
Weak
Vulnerable
Relevant powers
```

## Enemies

For each enemy:

``` text
HP / Max HP
Block
Alive/dead
Intent type
Intent damage
Number of hits
Strength
Weak
Vulnerable
Relevant powers
```

## Combat

``` text
Turn number
Number of living enemies
Cards remaining in draw pile
Cards in discard pile
Cards exhausted
```

## Cards

The state also needs information about:

-   Current hand
-   Draw pile composition
-   Discard pile
-   Exhaust pile

The first implementation does not need to perfectly encode every obscure
mechanic. The representation can evolve as the solver encounters cases
where important information is missing.

------------------------------------------------------------------------

# Avoid Encoding Hidden Future Information

The exact ordering of the draw pile deserves special consideration.

If the solver receives the exact future draw order, it may effectively
learn to exploit information that a normal player should not know.

A more transferable representation could encode the **composition** of
the draw pile rather than its exact order:

``` text
Draw pile:

2 Strike
2 Defend
1 Bash
1 Pommel Strike
...
```

This lets the network reason about probabilities and remaining resources
without memorizing exact future draws.

It also reduces state complexity and makes similar states easier to
generalize between.

Whether exact RNG information should be available is ultimately a design
choice:

### Perfect-information solver

The solver knows the exact RNG and future draw order.

Goal:

> Find the best sequence for this exact deterministic future.

### Player-information solver

The solver only receives information that a real player could reasonably
know.

Goal:

> Find the action with the best expected result across possible futures.

The second approach is generally more interesting for recommendations
and produces knowledge that transfers better between combats.

------------------------------------------------------------------------

# Card Representation and Embeddings

Cards eventually need a representation that allows knowledge to transfer
between states.

A simple first implementation could use explicit card features:

``` text
Cost
Damage
Block
Draw
Vulnerable
Weak
Exhaust
Target type
etc.
```

Eventually, card identities can also use learned **embeddings**.

An embedding converts a card identity into a small learned vector:

``` text
Bash →
[0.71, -0.12, 0.38, 0.91, ...]

Strike →
[0.54, -0.08, 0.61, 0.11, ...]

Defend →
[-0.17, 0.83, 0.04, 0.29, ...]
```

The individual numbers do not have predefined meanings. Training adjusts
them so that the network can represent useful relationships between
cards.

This is attractive for cross-combat learning because adding one new card
to the deck does not invalidate everything learned about the other
cards.

------------------------------------------------------------------------

# Variable-Length State

Slay the Spire contains many variable-length collections:

-   Hand
-   Draw pile
-   Discard pile
-   Exhaust pile
-   Enemies
-   Powers
-   Relics
-   Status effects

A long-term architecture may therefore use separate encoders:

``` text
Player Features ───────────────┐
                               │
Hand Cards ─→ Card Encoder ────┤
                               │
Draw Cards ─→ Card Encoder ────┤
                               ├─→ State Representation
Discard ────→ Card Encoder ────┤
                               │
Enemies ────→ Enemy Encoder ───┤
                               │
Powers ─────→ Power Encoder ───┘

Candidate Action
       │
       ▼
 Action Encoder
       │
       ▼

State Representation + Action Representation
                    │
                    ▼
                  MLP
                    │
                    ▼
              Predicted Value
```

This does not need to be the first implementation. A fixed feature
vector is a reasonable prototype.

The important architectural principle is to keep **game-state capture**
separate from **ML encoding**, so the representation can evolve without
rewriting the simulator.

------------------------------------------------------------------------

# Simulation Experience

Every complete simulation generates more than one training example.

Suppose a simulation makes these decisions:

``` text
State 0 → Bash
State 1 → Defend
State 2 → Strike
State 3 → End Turn
...
State 13 → Strike

Result:
Win with 31 HP
```

That simulation produces experiences such as:

``` text
(State 0, Bash)      → final outcome
(State 1, Defend)    → final outcome
(State 2, Strike)    → final outcome
(State 3, End Turn)  → final outcome
...
```

If one simulation contains roughly 15 decisions:

``` text
500 simulations × 15 decisions
≈ 7,500 experiences per combat
```

After 20 combats:

``` text
≈ 150,000 experiences
```

Therefore 400--1000 complete simulations per combat can still generate a
substantial training dataset.

------------------------------------------------------------------------

# Reward

The solver needs a numerical definition of a good outcome.

A simple starting reward could heavily distinguish wins from losses
while also valuing remaining HP.

For example:

``` text
Loss             → negative reward
Win              → large positive reward
Remaining HP     → additional positive reward
```

Conceptually:

``` csharp
if (!result.Won)
    return -1.0f;

return 1.0f + result.HpRemaining / result.MaxHp;
```

The exact values can be tuned later.

Initially, simple rewards are preferable to complicated handcrafted
scoring.

Potential future considerations include:

-   Remaining HP
-   Win/loss
-   Turns taken
-   Resources preserved
-   Temporary versus permanent costs
-   Potion usage
-   Special combat objectives

The reward function determines what "good play" means, so changes should
be made carefully.

------------------------------------------------------------------------

# Credit Assignment

An early implementation can use the final simulation outcome for every
decision made during that simulation.

For example:

``` text
Bash → Defend → Strike → ... → Win at 60% HP
```

Each chosen action receives that eventual outcome as its training
target.

This is a form of **Monte Carlo return**.

It is simple and suitable for a first implementation.

Later, the system could use temporal-difference methods that estimate
future value from intermediate states, but this adds complexity and is
not necessary for proving the concept.

------------------------------------------------------------------------

# Exploration vs Exploitation

Once a model begins learning, always choosing its currently
highest-rated action creates a dangerous feedback loop.

Suppose:

``` text
Bash   predicted value = 0.72
Strike predicted value = 0.69
```

If the solver always chooses Bash, it gathers more Bash data but stops
learning about Strike.

Strike might actually be better.

Therefore simulation policy needs both:

-   **Exploitation:** use actions currently believed to be good.
-   **Exploration:** deliberately test uncertain alternatives.

A simple initial strategy is epsilon-greedy:

``` text
Early learning:

70% model-guided
30% exploratory
```

Later:

``` text
95% model-guided
5% exploratory
```

Another option is sampling actions according to their predicted values
rather than always selecting the maximum.

The exact exploration strategy can evolve later.

------------------------------------------------------------------------

# Why Pure MCTS Is Not the Primary Recommendation

Monte Carlo Tree Search is attractive because SpireSolver can simulate
combat.

MCTS repeatedly:

1.  Selects promising branches.
2.  Expands unexplored actions.
3.  Simulates outcomes.
4.  Propagates rewards backward.

However, several properties of this project make pure MCTS less
attractive as the core architecture:

-   Only approximately 400--1000 rollouts are available.
-   STS has a large branching factor.
-   The game is stochastic.
-   Exact states diverge rapidly because of cards and RNG.
-   Knowledge should transfer between combats.
-   The deck changes gradually rather than resetting completely.

A persistent learned value model addresses these requirements better.

MCTS or another shallow search method could still be added later, with
the neural model guiding which branches deserve the limited simulation
budget.

That creates a potentially powerful combination:

``` text
Persistent Neural Knowledge
          ↓
Guided Search
          ↓
Current-Combat Simulations
          ↓
Better Current Decision
```

------------------------------------------------------------------------

# Memory Usage

The main memory risk is not having a `CombatSnapshot` class.

The dangerous approach would be storing complete game objects or exact
state dictionaries indefinitely.

Instead, experiences should store compact encoded data.

Conceptually:

``` csharp
public sealed class Experience
{
    public float[] State;
    public float[] Action;
    public float Reward;
}
```

If an encoded state/action pair contained 500 floats:

``` text
500 × 4 bytes ≈ 2 KB
```

Even then, the solver does not need to retain every experience forever.

Use a bounded **experience replay buffer**.

For example:

``` text
Maximum experiences: 50,000
```

New experiences are added while older examples are discarded or sampled
out.

This keeps memory usage bounded regardless of how long the run lasts.

The replay buffer can also preserve a mixture of experiences from
previous combats, which helps prevent the model from immediately
forgetting earlier knowledge.

------------------------------------------------------------------------

# Suggested Software Architecture

The simulation system should be separated into several responsibilities.

## CombatSnapshot

Captures the strategically relevant current game state.

``` csharp
CombatSnapshot CaptureCurrentState();
```

This layer understands STS game objects.

It should not contain neural-network logic.

------------------------------------------------------------------------

## StateEncoder

Converts a `CombatSnapshot` into the representation consumed by the
model.

``` csharp
float[] Encode(CombatSnapshot snapshot);
```

This separation is important because the state representation will
almost certainly change during development.

------------------------------------------------------------------------

## CombatAction

Represents one legal decision.

Examples:

``` text
PlayCard(card, target)
EndTurn
```

Potential future actions can be added without changing the fundamental
policy interface.

------------------------------------------------------------------------

## ActionGenerator

Given the current state, generates all currently legal actions.

``` csharp
IReadOnlyList<CombatAction> GetLegalActions();
```

It should not enumerate complete future turn sequences.

------------------------------------------------------------------------

## DecisionPolicy

Chooses among legal actions.

Conceptually:

``` csharp
public interface IDecisionPolicy
{
    CombatAction ChooseAction(
        CombatSnapshot state,
        IReadOnlyList<CombatAction> legalActions);
}
```

Implementations might include:

``` text
RandomPolicy
HeuristicPolicy
NeuralPolicy
ExplorationPolicy
```

This lets the existing random behavior remain available for testing.

------------------------------------------------------------------------

## Experience

Records a decision for later learning.

Conceptually:

``` csharp
public sealed class Experience
{
    public CombatSnapshot State;
    public CombatAction Action;
    public float Reward;
}
```

For persistent storage or replay, the snapshot/action can instead be
stored in encoded numeric form.

------------------------------------------------------------------------

## ReplayBuffer

Stores a bounded collection of experiences from current and previous
combats.

Responsibilities include:

-   Adding new experiences
-   Maintaining a maximum capacity
-   Randomly sampling training batches
-   Preserving cross-combat knowledge
-   Avoiding unbounded memory growth

------------------------------------------------------------------------

## Learned Model

Predicts:

``` text
Q(state, action)
```

The policy evaluates every currently legal candidate action and chooses
according to the exploration strategy.

------------------------------------------------------------------------

# Proposed Development Roadmap

## Phase 1 --- Refactor Actions

Do not introduce ML yet.

Extract Autoplayer's choices into a generic action system.

Instead of:

``` text
Find playable cards
Choose random card
Play it
```

move toward:

``` text
Generate legal actions
Choose action through DecisionPolicy
Execute action
```

Include `EndTurn` as a normal action.

Keep `RandomPolicy` so existing behavior remains functional.

------------------------------------------------------------------------

## Phase 2 --- Record Experiences

Before each decision, capture:

``` text
Current state
Legal actions
Chosen action
```

When the simulation finishes, attach:

``` text
Won/lost
Remaining HP
Reward
```

At this stage the solver is still random, but it begins producing a
useful learning dataset.

------------------------------------------------------------------------

## Phase 3 --- Build CombatSnapshot

Identify the combat information needed to make meaningful decisions.

Start with obvious high-value features:

-   HP
-   Block
-   Energy
-   Hand
-   Draw/discard/exhaust composition
-   Enemy HP
-   Enemy intents
-   Common powers
-   Turn number

Do not attempt to perfectly encode every mechanic immediately.

------------------------------------------------------------------------

## Phase 4 --- Build State and Action Encoders

Convert game objects into compact numeric inputs.

Keep encoding code independent from the simulator.

Start simple.

Complex embeddings and variable-length neural architectures can be
introduced after the pipeline works.

------------------------------------------------------------------------

## Phase 5 --- Train a Small Value Network

Build a model that estimates:

``` text
Q(state, action)
```

Input:

``` text
Encoded state
+
Encoded candidate action
```

Output:

``` text
One predicted value
```

Initially train from complete simulation outcomes.

------------------------------------------------------------------------

## Phase 6 --- Neural-Guided Simulation

Replace purely random choices with a mixture of:

``` text
Model-guided decisions
+
Random exploration
```

Now later simulations within the same combat should increasingly
concentrate on promising strategies.

------------------------------------------------------------------------

## Phase 7 --- Persist Knowledge Between Combats

Keep:

-   Learned model parameters
-   A bounded replay buffer

between combats.

As the deck changes gradually, the model continues learning rather than
restarting from zero.

This is the key step that turns the project from a current-combat
optimizer into a solver that develops knowledge throughout a run.

------------------------------------------------------------------------

## Phase 8 --- Improve Exploration and Search

Once the learned policy works reliably, use the limited 400--1000
simulations more intelligently.

Possible later additions include:

-   Confidence/uncertainty estimates
-   Softmax exploration
-   Prioritized experience replay
-   Shallow tree search
-   Neural-guided MCTS
-   Separate policy and value networks

These should come after the basic learning pipeline is proven.

------------------------------------------------------------------------

# Long-Term Vision

The final system could behave approximately like this:

``` text
                    START RUN
                        │
                        ▼
                Weak / fresh model
                        │
                        ▼
                    Combat 1
                        │
                 500 simulations
                        │
                        ▼
                 Learn deck behavior
                        │
                        ▼
                    Combat 2
                        │
             Start with prior knowledge
                        │
                 500 simulations
                        │
                        ▼
                 Refine understanding
                        │
                        ▼
                    Combat 3
                        │
                       ...
                        │
                        ▼
                Increasingly capable
                  deck-specific model
```

For an individual decision:

``` text
                Current Combat State
                         │
                         ▼
                  Capture Snapshot
                         │
                         ▼
                Generate Legal Actions
                         │
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
        Bash           Defend        End Turn
          │              │              │
          └──────────────┼──────────────┘
                         ▼
                 Evaluate Q(s, a)
                         │
                         ▼
              Exploration / Selection
                         │
                         ▼
                   Execute Action
                         │
                         ▼
                    Next State
```

And after a simulation:

``` text
Decision Experiences
        +
Final Combat Result
        ↓
Calculate Rewards
        ↓
Replay Buffer
        ↓
Train Model
        ↓
Improved Predictions
```

------------------------------------------------------------------------

# Central Design Principle

The solver should **not attempt to enumerate Slay the Spire**.

The state and action spaces are too large, and only hundreds of complete
simulations are available for each combat.

Instead:

> Use simulation to generate experience, use learning to generalize that
> experience, preserve the learned knowledge between combats, and use
> that knowledge to spend future simulations on increasingly promising
> decisions.

The simulator provides ground truth about what happened.

The learned model provides generalization.

Exploration prevents the model from becoming trapped by its early
assumptions.

Persistent training allows knowledge about the mostly-stable deck to
accumulate across the run.

Together, these components provide a realistic path from the current
random Autoplayer toward a combat solver that gradually develops an
understanding of how to play the player's particular deck.
