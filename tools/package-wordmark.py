from PIL import Image
im=Image.open('assets/wordmark-original.png').convert('RGBA');bounds=im.getchannel('A').point(lambda a:255 if a>128 else 0).getbbox();im=im.crop(bounds);im.save('src/LingNote/Assets/wordmark.png')
print('Wordmark:',im.size,im.getchannel('A').getextrema())
