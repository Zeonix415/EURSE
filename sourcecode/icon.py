from PIL import Image, ImageDraw

canvas = Image.new("RGBA", (256, 256))
draw = ImageDraw.Draw(canvas)
draw.rounded_rectangle((8, 8, 248, 248), 46, fill="#15212f", outline="#344f61", width=8)
draw.arc((51, 51, 205, 205), 35, 324, fill="#64dedf", width=15)
draw.line([(76, 130), (102, 130), (121, 98), (144, 157), (160, 130), (191, 130)], fill="#e7f0f8", width=13, joint="curve")
draw.ellipse((183, 121, 201, 139), fill="#64dedf")
canvas.save("work/EUSRE.ico", sizes=[(16,16), (24,24), (32,32), (48,48), (64,64), (128,128), (256,256)])
