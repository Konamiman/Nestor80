# Nestor80 assembler language reference

This documents details the source file format supported by Nestor80 and lists all the available assembler instructions (called "pseudo-operators" in the MACRO-80 manual), 
both the ones inherited from MACRO-80 and the ones newly introduced by Nestor80.


## Document conventions

The following icons are used in this document:

🆕 A "new" icon is used when introducing a feature or instruction that is new in Nestor80 (it wasn't available in MACRO-80).

✨ A "sparks" icon is used when referring to a feature or instruction that was already available in MACRO-80 but has been enhanced or improved in a backwards-compatible way in Nestor80.

🚫 A "forbidden" icon is used to refer to a MACRO-80 feature or instruction that is not available or has changed in a backwards-incompatible way in Nestor80.
 Old code intended for MACRO-80 and relying in such features or instructions will likely require changes before being assembled with Nestor80.

⚠ A "warning" icon is used when discussing a tricky, subtle or confusing subject.


