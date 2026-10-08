# MBTI Personalities: setting types by hand

## Save files

This mod stores each girl's MBTI type in the `Variables` parameter of her entry in the save file. To change a type, find `Variables` and edit the type in it:

```json
"Variables": [
    "ENFJ"
],
```

The old `_mbti.json` files are obsolete and safe to delete.

## Unique idols

To give a unique idol a specific MBTI type, add this line to her `params.json`:

```json
"mbti": "INTJ"
```
