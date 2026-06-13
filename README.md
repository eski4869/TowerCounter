# Tower Counter

Tower Counter is a simple mod that counts and displays how many times you have reached a tower entrance.

Press `T` on the tower entrance screen to start counting from `Tower: 1`. After that, the counter increases when you leave the tower area and later return to the marked entrance screen.

The counter is shown in-game near the timer. You can turn it on or off from the `Tower Counter` menu checkbox.

The current count is kept when you close the game and continue later.

<img width="500" height="360" alt="image" src="https://github.com/user-attachments/assets/bf1a7edf-3ae8-4893-ae12-af82244a12c7" />

## Controls

| Key | Action |
| --- | --- |
| `T` | Mark the current screen and area as the tower entrance. The count is reset to `1`. |
| `+` | Increase the count by `1`. |
| `-` | Decrease the count by `1`. The count will not go below `0`. |

## Menu

| Item | Action |
| --- | --- |
| `Tower Counter` | Enables or disables the counter. Counter detection is paused while this is off. |

## Counter Logic

When you press `T`, the mod stores the current screen and current area as the tower entrance.

After that, the mod waits until you leave the marked tower area. When you later return to the marked entrance screen and land there, the count increases by `1`.

Pressing `T` somewhere else replaces the previous tower mark and starts again from `Tower: 1`.
