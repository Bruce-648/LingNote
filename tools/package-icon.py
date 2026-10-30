from PIL import Image
from pathlib import Path
p=Path('src/LingNote/Assets')
original=Image.open(p/'app-ghost-original.png').convert('RGBA')
bounds=original.getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
if not bounds: raise ValueError('Icon is empty')
mark=original.crop(bounds)
# Preserve aspect ratio; allocate almost all of the ICO canvas to the visible mark.
edge=1024
mark.thumbnail((984,984),Image.Resampling.LANCZOS)
canvas=Image.new('RGBA',(edge,edge),(0,0,0,0))
canvas.alpha_composite(mark,((edge-mark.width)//2,(edge-mark.height)//2))
canvas.save(p/'app-logo.png')
canvas.save(p/'app.ico',sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(128,128),(256,256)])
print('Original visible height:',bounds[3]-bounds[1],'of',original.height)
print('New visible height:',mark.height,'of',edge)
