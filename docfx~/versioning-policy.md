## Versioning policy

NDMF generally complies with [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html). Versions are expressed as
a MAJOR.MINOR.PATCH version triplet.

* The major version will be incremented when non-backwards-compatible changes are made. For example, removing API
  members, or adding abstract methods to abstract classes.
* The minor version will be incremented when backwards-compatible changes are made. For example, adding new API members
  to a class, or adding new abstract classes. It will also be incremented when changes to execution order heuristics
  are made.
* The patch version will be incremented when backwards-compatible bug fixes are made.

Execution order heuristics are used to resolve the order of execution of passes when constraints are not sufficient to
fully determine the order. For example, if two passes are declared to run after a third pass, but no other constraints
are declared, the order of execution of the two passes is not fully determined. In this case, NDMF will use a heuristic
to determine the order of execution. NDMF will strive not to change this heuristic between patch releases, but it may
change on minor releases. If you run into problems with changes in a heuristic, please constrain the order of execution
of your passes explicitly.

**Versions before 1.0.0**: Versions starting with 0.x.y are considered to be unstable and do not guarantee API stability.
We will make a best-effort attempt to increment the "x" version when making incompatible changes.